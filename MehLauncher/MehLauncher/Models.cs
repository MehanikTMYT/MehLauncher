namespace MehLauncher.Models
{
    public class AuthResponseData
    {
        public string AccessToken { get; set; }
        public string UserUUID { get; set; }
        public string Username { get; set; }
        public bool IsAlex { get; set; }
        public string SkinUrl { get; set; }
        public string CapeUrl { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Result { get; set; }
        public string Error { get; set; }
    }
    public class FileData
    {
        public string FilePath { get; set; }
        public string Hash { get; set; }
    }
}
