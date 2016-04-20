using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace HTM.Net.Research.Vision.Sensor.Explorers
{
    /// <summary>
    /// This explorer flashes each image nine times, shifted by one pixel each time,
    /// simulating "eye movements".
    /// </summary>
    public class EyeMovements : BaseExplorer
    {
        /// <summary>
        /// Number of pixels to move from the center ("radius" of the eye movement square).
        /// </summary>
        private int _shift;
        /// <summary>
        ///  A function that's used by inference analysis to
        /// aggregate the results of different eye movement presentations.Valid
        /// values are 'sum', 'average', 'product' and 'max'. The default is 'sum'.
        /// </summary>
        private string _aggregate;

        private int _eyeMovementIndex;

        public EyeMovements()
        {
            _shift = 1;
            _aggregate = "sum";
        }

        /// <summary>
        /// Set up the position.
        /// 
        /// BaseExplorer picks image 0, offset(0,0), etc., but explorers that wish
        /// to set a different first position should extend this method.Such explorers
        /// may wish to call BaseExplorer.first(center=False), which initializes the
        /// position tuple but does not call centerImage() (which could cause
        /// unnecessary filtering to occur).
        /// </summary>
        /// <param name="center"></param>
        public override void First(bool center = true)
        {
            base.First(false);

            _eyeMovementIndex = 0;
        }

        /// <summary>
        /// Go to the next position (next iteration).
        /// </summary>
        /// <param name="seeking">Boolean that indicates whether the explorer is calling next()
        ///   from seek(). If True, the explorer should avoid unnecessary computation
        ///   that would not affect the seek command. The last call to next() from
        ///   seek() will be with seeking=False.</param>
        public override void Next(bool seeking = false)
        {
            Debug.WriteLine("> Executing Next() for EyeMovements on image {0}, eye position {1}",
                _position.Image, _eyeMovementIndex);
            //  Iterate through eye movement positions
            _eyeMovementIndex += 1;
            if (_eyeMovementIndex < 9)
            {
                CenterImage();
                if (new[] {1, 2, 3}.Contains(_eyeMovementIndex))
                {
                    _position.Offset = new Point(_position.Offset.Value.X, _position.Offset.Value.Y - _shift);
                }
                else if (new[] {5, 6, 7}.Contains(_eyeMovementIndex))
                {
                    _position.Offset = new Point(_position.Offset.Value.X, _position.Offset.Value.Y + _shift);
                }
                else if (new[] {1, 7, 8}.Contains(_eyeMovementIndex))
                {
                    _position.Offset = new Point(_position.Offset.Value.X - _shift, _position.Offset.Value.Y);
                }
                else if (new[] {3, 4, 5}.Contains(_eyeMovementIndex))
                {
                    _position.Offset = new Point(_position.Offset.Value.X + _shift, _position.Offset.Value.Y);
                }
                _position.Reset = false;
            }
            else
            {
                _eyeMovementIndex = 0;
                // Iterate through the filters
                for (int i = 0; i < _numFilters; i++)
                {
                    _position.Filters[i] += 1;
                    if (_position.Filters[i] < _numFilterOutputs[i])
                    {
                        CenterImage();
                        return;
                    }
                    _position.Filters[i] = 0;
                }
                // Go to next image
                _position.Image += 1;
                if (_position.Image == _numImages)
                {
                    _position.Image = 0;
                }
                _position.Reset = true;
                CenterImage();
            }
        }

        public override int GetNumIterations(int? image)
        {
            if (image.HasValue)
            {
                return NumFilteredVersionsPerImage*9;
            }
            return NumFilteredVersionsPerImage * 9 * _numImages;
        }
    }
}