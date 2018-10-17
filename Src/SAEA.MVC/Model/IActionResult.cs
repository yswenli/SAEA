using System.Net;
using System.Text;

namespace SAEA.MVC.Model
{
    public interface IActionResult
    {
        string Content { get; set; }
        Encoding ContentEncoding { get; set; }
        string ContentType { get; set; }
        HttpStatusCode Status { get; set; }
    }
}