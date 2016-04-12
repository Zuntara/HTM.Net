namespace HTM.Net.Research.Vision.Sensor.Explorers
{
    public class ImageSweep : BaseExplorer
    {
        /// <summary>
        /// Go to the next position (next iteration).
        /// 
        /// seeking -- Boolean that indicates whether the explorer is calling next()
        ///   from seek(). If True, the explorer should avoid unnecessary computation
        ///   that would not affect the seek command. The last call to next() from
        ///   seek() will be with seeking=False.
        /// </summary>
        /// <param name="seeking"></param>
        public override void Next(bool seeking = false)
        {
            _position.Reset = false;
            var prevImage = _position.Image;

            // iterate through the filters
            for (int i = 0; i < _numFilters; i++)
            {
                _position.Filters[i] += 1;
                if (_position.Filters[i] < _numFilterOutputs[i])
                {
                    if (!seeking)
                    {
                        // center the image
                        CenterImage();
                    }
                    continue;
                }
                _position.Filters[i] = 0;
            }
            // Goto the next image
            _position.Image += 1;
            if (_position.Image == _numImages)
            {
                _position.Image = 0;
            }
            if (!seeking)
            {
                // center image
                CenterImage();
            }

            var image = _position.Image;
            if (string.IsNullOrWhiteSpace(_getImageInfo(prevImage).ImagePath)) // todo check directory change
            {
                _position.Reset = true;
            }
        }

        public override int GetNumIterations(int? image)
        {
            if (image.HasValue)
            {
                return NumFilteredVersionsPerImage;
            }
            return NumFilteredVersionsPerImage * _numImages;
        }
    }
}