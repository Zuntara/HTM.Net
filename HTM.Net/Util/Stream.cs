using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HTM.Net.Util
{
    public interface IStream
    {
        IEnumerator GetEnumerator();
        void IncrementCurrentlyRead();
        IStream GetParentStream();
        int Count();
    }

    public interface IBaseStream
    {
        /// <summary>
        /// Reads an entry from the stream as an object
        /// </summary>
        /// <returns></returns>
        object ReadUntyped();
        bool EndOfStream { get; }
        IBaseStream CopyUntyped();
    }

    public interface IStream<T>
    {
        event Action<bool> TerminalChanged;

        IStream<T> Filter(Func<T, bool> predicate);
        void ForEach(Action<T> action);
        IStream<TMapped> Map<TMapped>(Func<T, TMapped> mapping);
        void Write(T current);
        T Read();
        bool IsTerminal();
        void SetOffset(int offset);
        StreamState GetStreamState();
        /// <summary>
        /// Create a fanout copy stream of the current stream. This makes it possible to read the stream multiple times.
        /// </summary>
        /// <returns></returns>
        IStream<T> Copy();
        IEnumerator GetEnumerator();
        int Count();
        bool EndOfStream { get; }
    }

    [Serializable]
    internal class StreamIdentifier
    {
        public static int GeneralId = 0;
    }

    [Serializable]
    public class StreamState
    {
        public bool IsTerminal { get; internal set; }
        public bool IsTerminated { get; internal set; } // no extra items comming in
        public bool EndOfStream { get; internal set; }
        public int? Count { get; internal set; }
        public bool ObservableSource { get; internal set; }

        internal object SyncRoot = new object();
    }

    // TODO: create own iterator for collection for pushing observable stuff into it
    // to avoid collection modified errors and to avoid relooping the same data again and hitting the mappings too many.

    // New version for streaming
    [Serializable]
    public class Stream<TModel> : IStream<TModel>, IStream, IBaseStream
    {
        private IStream _parentStream;
        protected StreamState _streamState;
        protected readonly int _id;
        protected IStreamCollection<TModel> _baseStream;
        protected IEnumerator<TModel> _localEnumerator;
        private int _currentlyRead, _offset;
        private readonly List<FanOutStream<TModel>> _childStreams = new List<FanOutStream<TModel>>();
        private readonly List<IStream> _transformStreams = new List<IStream>();
        protected bool _observableMode;

        protected Stream()
        {
            // Used by derived classes
            _id = StreamIdentifier.GeneralId++;
        }

        public Stream(IEnumerable<TModel> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            _id = StreamIdentifier.GeneralId++;
            _baseStream = new StreamCollection<TModel>(_id, source, IncrementCurrentlyRead);
            _localEnumerator = _baseStream.GetEnumerator();
            _streamState = new StreamState();
        }

        private Stream(IEnumerable<TModel> source, IStream parentStream)
        {
            if (source == null) throw new ArgumentNullException("source");
            _parentStream = parentStream;
            _id = StreamIdentifier.GeneralId++;
            if (source is StreamCollection<TModel>)
            {
                _baseStream = source as StreamCollection<TModel>;
                _baseStream.SetAfterReadAction(IncrementCurrentlyRead); // Update to right level
            }
            else
            {
                _baseStream = new StreamCollection<TModel>(_id, source, IncrementCurrentlyRead);
            }
            _localEnumerator = _baseStream.GetEnumerator();
            _streamState = new StreamState();
        }

        public Stream(IObservable<TModel> input)
        {
            _id = StreamIdentifier.GeneralId++;
            LinkedList<TModel> obsContent = new LinkedList<TModel>();
            _baseStream = new StreamCollection<TModel>(_id, obsContent, IncrementCurrentlyRead);
            _localEnumerator = _baseStream.GetEnumerator();
            _streamState = new StreamState();
            _observableMode = true;
            _streamState.ObservableSource = true;
            input.Subscribe(item =>
            {
                //Debug.WriteLine(String.Format("> {2} Adding item to local cache in stream: {0} - currently read: {1}",
                //    item, _currentlyRead, _id));
                lock (_streamState.SyncRoot)
                {
                    obsContent.AddLast(item);
                }
            }, () => { _streamState.IsTerminated = true; });
        }

        public event Action<bool> TerminalChanged;

        public IStream<TModel> Filter(Func<TModel, bool> predicate)
        {
            Debug.WriteLine(_id + " > Adding filter");
            MakeTerminal();
            IEnumerable<TModel> filteredStream = _baseStream.Where(predicate);
            var output = new Stream<TModel>(filteredStream, this) { _streamState = _streamState };
            _transformStreams.Add(output);
            return output;
        }

        public void ForEach(Action<TModel> action)
        {
            // do an action for each item in the queue that's left
            Debug.WriteLine(_id + " > Doing foreach");

            MakeTerminal();

            TModel obj;
            while ((obj = Read()) != null)
            {
                action(obj);
            }
        }

        public IStream<TMapped> Map<TMapped>(Func<TModel, TMapped> mapping)
        {
            Debug.WriteLine(_id + " > Adding transformation");
            IEnumerable<TMapped> mappedLazyStream = _baseStream.Select(mapping);
            var output = new Stream<TMapped>(mappedLazyStream, this) { _streamState = _streamState };
            _transformStreams.Add(output);
            return output;
        }

        public IEnumerator GetEnumerator()
        {
            return _localEnumerator;
        }

        public void Write(TModel current)
        {
            throw new NotSupportedException("This stream does not support writing to!");
        }

        public virtual TModel Read()
        {
            lock (_streamState.SyncRoot)
            {
                //UpdateEnumerator();
                if (_localEnumerator.MoveNext())
                {
                    TModel val = _localEnumerator.Current;
                    //Debug.WriteLine("> " + _id + " Reading stream from " + _localEnumerator.GetType().Name + " value: " + val);
                    // Update child streams if there are some (for fanout items)
                    UpdateChildStreams(val);
                    return val;
                }
            }
            if (_streamState.IsTerminated && (_observableMode || _streamState.ObservableSource))
            {
                Debug.WriteLine(">" + _id + " Reading stream EOS reached for " + _localEnumerator.GetType().Name);
                _streamState.EndOfStream = true;
            }
            else if (!_observableMode && !_streamState.ObservableSource)
            {
                Debug.WriteLine(">" + _id + " Reading stream EOS reached for " + _localEnumerator.GetType().Name);
                _streamState.EndOfStream = true;
            }
            return default(TModel);
        }

        public object ReadUntyped()
        {
            return Read();
        }

        public TModel First()
        {
            if (!IsTerminal())
            {
                MakeTerminal();
            }
            return _baseStream.FirstOrDefault();
        }

        public bool IsTerminal()
        {
            return _streamState.IsTerminal;
        }

        public bool EndOfStream
        {
            get { return _streamState.EndOfStream; }
        }

        public void MakeTerminal()
        {
            DoStreamReadingForTermination();
            _streamState.IsTerminal = true;
            TerminalChanged?.Invoke(true);
        }

        public void SetOffset(int offset)
        {
            _offset = offset;
            _baseStream.SetOffsetting(offset);
        }

        public int Count()
        {
            if (!IsTerminal())
            {
                MakeTerminal();
            }
            // recurse further down and take max value of currently read
            if (_parentStream != null)
            {
                return Math.Max(_currentlyRead - _offset, _parentStream.Count());
            }
            return _currentlyRead - _offset;
        }

        private void DoStreamReadingForTermination()
        {
            lock (_streamState.SyncRoot)
            {
                _baseStream.Terminate();
                _streamState.Count = _currentlyRead;
            }
        }

        private void UpdateChildStreams(TModel val)
        {
            if (_childStreams.Any())
            {
                _childStreams.ForEach(cs =>
                {
                    cs.Write(val);
                });
            }
        }

        public void IncrementCurrentlyRead()
        {
            //Debug.WriteLine("{0} > Incrementing read count with one.", _id);
            _currentlyRead++;
        }

        public IStream GetParentStream()
        {
            return _parentStream;
        }

        public StreamState GetStreamState()
        {
            return _streamState;
        }

        public IStream<TModel> Copy()
        {
            // Create a fanout stream
            var fanOut = new FanOutStream<TModel>(this, _streamState);
            _childStreams.Add(fanOut);
            return fanOut;
        }

        public IBaseStream CopyUntyped()
        {
            // Create a fanout stream
            var fanOut = new FanOutStream<TModel>(this, _streamState);
            _childStreams.Add(fanOut);
            return fanOut;
        }
    }

    public class ComputedStream<TModel> : Stream<TModel>
    {
        public ComputedStream(IEnumerable<TModel> source, Action beforeReadAction)
        {
            if (source == null) throw new ArgumentNullException("source");
            _baseStream = new ComputedStreamCollection<TModel>(_id, source, beforeReadAction, IncrementCurrentlyRead);
            _localEnumerator = _baseStream.GetEnumerator();
            _streamState = new StreamState();
        }

        public ComputedStream(IObservable<TModel> input, Action beforeReadAction)
        {
            LinkedList<TModel> obsContent = new LinkedList<TModel>();
            _baseStream = new ComputedStreamCollection<TModel>(_id, obsContent, beforeReadAction, IncrementCurrentlyRead);
            _localEnumerator = _baseStream.GetEnumerator();
            _streamState = new StreamState();
            _observableMode = true;
            _streamState.ObservableSource = true;
            input.Subscribe(item =>
            {
                //Debug.WriteLine(String.Format("> {2} Adding item to local cache in stream: {0} - currently read: {1}",
                //    item, _currentlyRead, _id));
                lock (_streamState.SyncRoot)
                {
                    obsContent.AddLast(item);
                }
            }, () => { _streamState.IsTerminated = true; });
        }

        public override TModel Read()
        {
            Debug.WriteLine("Executing read on ComputedStream");
            return base.Read();
        }
    }

    public class FanOutStream<TModel> : IStream<TModel>, IBaseStream
    {
        private StreamState _streamState;
        private readonly int _id;
        private readonly Stream<TModel> _parentStream;
        public event Action<bool> TerminalChanged;
        private LinkedList<TModel> _list;
        private int _offset;

        internal FanOutStream(Stream<TModel> parentStream, StreamState streamState)
        {
            _id = StreamIdentifier.GeneralId++;
            _parentStream = parentStream;
            _list = new LinkedList<TModel>();
            _streamState = streamState;
        }

        public IStream<TModel> Filter(Func<TModel, bool> predicate)
        {
            throw new NotImplementedException("Filter not supported on fanout streams at the moment");
        }

        public void ForEach(Action<TModel> action)
        {
            throw new NotImplementedException("ForEach not supported on fanout streams at the moment");
        }

        public IStream<TMapped> Map<TMapped>(Func<TModel, TMapped> mapping)
        {
            throw new NotImplementedException("Mapping not supported on fanout streams at the moment");
        }

        public IEnumerator GetEnumerator()
        {
            throw new NotSupportedException("No need to get an enumerator here!");
        }

        public void Write(TModel current)
        {
            _list.AddLast(current);
        }

        public TModel Read()
        {
            //Debug.WriteLine("> " + _id + " Reading from FanOut Stream");
            // Read from the parent stream and take first item from list, then remove it (feed forward)
            if (!_list.Any() && !_streamState.EndOfStream)
            {
                _parentStream.Read();
            }
            var value = _list.FirstOrDefault();
            if (value != null)
            {
                _list.RemoveFirst();
            }
            return value;
        }

        public object ReadUntyped()
        {
            return Read();
        }

        public bool IsTerminal()
        {
            return _parentStream.IsTerminal();
        }

        public void SetOffset(int offset)
        {
            _offset = offset;
        }

        public int Count()
        {
            return _parentStream.Count();
        }

        public bool EndOfStream
        {
            get { return _parentStream.EndOfStream; }
        }

        public IBaseStream CopyUntyped()
        {
            throw new NotSupportedException("Create a stream from the parent stream!");
        }

        public TModel First()
        {
            return _parentStream.First();
        }

        public StreamState GetStreamState()
        {
            return _streamState;
        }

        public IStream<TModel> Copy()
        {
            throw new NotSupportedException("Create a stream from the parent stream!");
        }
    }

    public interface IStreamCollection<TModel> : IEnumerable<TModel>, IEnumerator<TModel>
    {
        void SetAfterReadAction(Action afterReadAction);
        void SetOffsetting(int offset);
        void Terminate();
        IEnumerable<TResult> Select<TResult>(Func<TModel, TResult> select);
    }

    [Serializable]
    public class StreamCollection<TModel> : IStreamCollection<TModel>
    {
        protected readonly int _streamId;
        protected Action _afterRead;
        private readonly LinkedList<TModel> _source;
        protected readonly bool _lazyRead;
        private readonly IEnumerator<TModel> _lazyEnumerator;
        protected int _offsetting;

        protected StreamCollection(int streamId, Action afterRead = null)
        {
            _lazyRead = true;
            _streamId = streamId;
            _afterRead = afterRead;
        }

        public StreamCollection(int streamId, IEnumerable<TModel> source, Action afterRead = null)
        {
            Debug.WriteLine(streamId + " Creating stream collection from enum list (pull)");
            _lazyRead = true;
            _streamId = streamId;
            _afterRead = afterRead;
            _source = new LinkedList<TModel>();
            _lazyEnumerator = source.GetEnumerator();
        }

        public StreamCollection(int streamId, LinkedList<TModel> source, Action afterRead = null)
        {
            Debug.WriteLine(streamId + " Creating stream collection from linked list (obs)");
            _streamId = streamId;
            _source = source;
            _afterRead = afterRead;
            _lazyRead = false;
        }

        public virtual void Terminate()
        {
            // Pull all elements from the lazy enumerator into our linked list
            if (_lazyRead)
            {
                Debug.WriteLine(_streamId + " > Terminating stream");
                while (_lazyEnumerator.MoveNext())
                {
                    _source.AddLast(_lazyEnumerator.Current);
                }
            }
        }

        public IEnumerator<TModel> GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {

        }

        public virtual bool MoveNext()
        {
            //Debug.WriteLine(_streamId + " > Moving to next item, lazy = " + _lazyRead + " type = " + _lazyEnumerator?.GetType().Name);
            if (_lazyRead)
            {
                // Pull from enumerable to linked list first
                while (_offsetting > 0)
                {
                    _lazyEnumerator.MoveNext();
                    _offsetting--;
                }

                if (_lazyEnumerator.MoveNext())
                {
                    _source.AddLast(_lazyEnumerator.Current);
                }
            }

            // Pull from linked list
            if (_source.Any())
            {
                Current = _source.First.Value;
                _source.RemoveFirst();
                _afterRead?.Invoke();
                Count++;
                return true;
            }

            Current = default(TModel);
            return false;
        }

        public IEnumerable<TResult> Select<TResult>(Func<TModel, TResult> select)
        {
            // Create new streamcollection and apply select criteria
            StreamCollection<TResult> collection = new MappedStreamCollection<TModel, TResult>(
                StreamIdentifier.GeneralId++, this, select, _afterRead);
            return collection;
        }

        public void Reset()
        {
            throw new NotSupportedException("We cannot reset the streaming collection");
        }

        public TModel Current { get; protected set; }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public int Count { get; protected set; }

        public void SetOffsetting(int offset)
        {
            _offsetting = Math.Max(0, offset - Count);
        }

        public void SetAfterReadAction(Action incrementCurrentlyRead)
        {
            _afterRead = incrementCurrentlyRead;
        }
    }

    [Serializable]
    public class MappedStreamCollection<TFrom, TTo> : StreamCollection<TTo>
    {
        private readonly LinkedList<TTo> _source;
        private readonly Func<TFrom, TTo> _select;
        private readonly IEnumerator<TFrom> _lazyEnumerator;

        public MappedStreamCollection(int streamId, IEnumerable<TFrom> source, Func<TFrom, TTo> select, Action afterRead = null)
            : base(streamId, afterRead)
        {
            Debug.WriteLine(streamId + " Creating stream collection from enum list (mapped pull)");
            _source = new LinkedList<TTo>();
            _select = select;
            _lazyEnumerator = source.GetEnumerator();
        }

        public override bool MoveNext()
        {
            //Debug.WriteLine(_streamId + " > Moving to next item (mapped), lazy = " + _lazyRead + " type = " + _lazyEnumerator?.GetType().Name);
            if (_lazyRead)
            {
                // Pull from enumerable to linked list first
                while (_offsetting > 0)
                {
                    _lazyEnumerator.MoveNext();
                    _offsetting--;
                }

                if (_lazyEnumerator.MoveNext())
                {
                    _source.AddLast(_select(_lazyEnumerator.Current));
                }
            }

            // Pull from linked list
            if (_source.Any())
            {
                Current = _source.First.Value;
                _source.RemoveFirst();
                _afterRead?.Invoke();
                Count++;
                return true;
            }

            Current = default(TTo);
            return false;
        }

        public override void Terminate()
        {
            // Pull all elements from the lazy enumerator into our linked list
            if (_lazyRead)
            {
                Debug.WriteLine(_streamId + " > Terminating stream");
                while (_lazyEnumerator.MoveNext())
                {
                    _source.AddLast(_select(_lazyEnumerator.Current));
                }
            }
        }
    }

    public class ComputedStreamCollection<TModel> : StreamCollection<TModel>
    {
        private readonly Action _beforeRead;

        protected ComputedStreamCollection(int streamId, Action beforeRead, Action afterRead = null)
            : base(streamId, afterRead)
        {
            _beforeRead = beforeRead;
        }

        public ComputedStreamCollection(int streamId, IEnumerable<TModel> source, Action beforeRead, Action afterRead = null)
            : base(streamId, source, afterRead)
        {
            _beforeRead = beforeRead;
        }

        public ComputedStreamCollection(int streamId, LinkedList<TModel> source, Action beforeRead, Action afterRead = null)
            : base(streamId, source, afterRead)
        {
            _beforeRead = beforeRead;
        }

        public override bool MoveNext()
        {
            _beforeRead?.Invoke();
            return base.MoveNext();
        }
    }
}