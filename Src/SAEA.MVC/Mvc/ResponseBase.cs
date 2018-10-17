using SAEA.MVC.Http;
using SAEA.MVC.Model;

namespace SAEA.MVC.Mvc
{
    public class ResponseBase : HttpResponse
    {
        /// <summary>
        /// 设置回复内容
        /// </summary>
        /// <param name="result"></param>
        internal void SetResult(IActionResult result)
        {
            this.Status = result.Status;
            if (result is EmptyResult)
            {
                return;
            }
            else if (result is FileResult)
            {
                var fileResult = (FileResult)result;
                this.ContentType = fileResult.ContentType;
                this.SetContent(fileResult.Content);
            }
            else
            {
                this.ContentType = result.ContentType;
                this.SetContent(result.Content);
            }
        }
    }
}
