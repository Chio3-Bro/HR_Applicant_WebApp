namespace MyHRExample.Service
{
    public class S3ConfigurationException : InvalidOperationException
    {
        public S3ConfigurationException(string setting)
            : base($"Upload is not configured. Set {setting} in User Secrets, then try again.")
        {
        }
    }
}
