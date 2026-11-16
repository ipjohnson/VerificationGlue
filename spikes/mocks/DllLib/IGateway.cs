namespace Vendor; public interface IGateway { string Send(string body); System.Threading.Tasks.Task<bool> Ping(); }
