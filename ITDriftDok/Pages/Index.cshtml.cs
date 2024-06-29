using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ITDriftDok.Pages
{

    public class IndexModel : PageModel
    {
        private readonly ILogger _logger;
        public WikiFolder[] Innhold { set; get; }
        public IndexModel(ILogger<IndexModel> logger, WikiFolder[] _innhold)
        {
            _logger = logger;
            Innhold = _innhold;
        }
        public void OnGet()
        {
            var path = HttpContext.Request.Path.Value;
            _logger.LogInformation("Index page visited at {DT} path: {1}",
                DateTime.Now.ToLongTimeString(), path);
        }
        public override void OnPageHandlerSelected(
                                    PageHandlerSelectedContext context)
        {
            _logger.LogInformation("IndexModel/OnPageHandlerSelected");
        }

        public override void OnPageHandlerExecuting(
                                    PageHandlerExecutingContext context)
        {
//            Message = "Message set in handler executing";
            _logger.LogInformation("IndexModel/OnPageHandlerExecuting");
        }


        public override void OnPageHandlerExecuted(
                                    PageHandlerExecutedContext context)
        {
            _logger.LogInformation("IndexModel/OnPageHandlerExecuted");
        }

    }
}
