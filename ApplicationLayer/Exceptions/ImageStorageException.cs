using System;

namespace Application.Exceptions
{
    public class ImageStorageException : Exception
    {
        public ImageStorageException(string message) : base(message)
        {
        }

        public ImageStorageException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class ImageValidationException : Exception
    {
        public ImageValidationException(string message) : base(message)
        {
        }
    }
}
