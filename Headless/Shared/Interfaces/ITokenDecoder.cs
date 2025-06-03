namespace Headless.Shared.Interfaces
{
    public interface ITokenDecoder
    {
        public Dictionary<string,string> DecodeToken(string token);
    }
}
