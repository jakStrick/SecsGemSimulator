namespace BasicSecsGemServer
{
    // Custom Exception
    public class HsmsException : Exception
    {
        public HsmsException(string message) : base(message) { }
        public HsmsException(string message, Exception innerException) : base(message, innerException) { }
    }
}