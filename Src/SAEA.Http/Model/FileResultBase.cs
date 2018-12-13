namespace SAEA.Http.Model
{
    public interface IFileResult: IHttpResult
    {        
        byte[] Content { get; set; }
    }
}
