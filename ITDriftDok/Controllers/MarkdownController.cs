using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
//using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Log4Net;
using Westwind.AspNetCore.Markdown;

namespace ITDriftDok.Controllers
{
    public class MarkdownController : Controller
    {
        private readonly ILogger _logger;
        private readonly IWebHostEnvironment hostingEnvironment;

        public MarkdownController(IWebHostEnvironment hostingEnvironment, ILogger<MarkdownController> logger)
        {
            _logger = logger;
            this.hostingEnvironment = hostingEnvironment;
        }

        [Route("markdown/markdownpage")]
        public async Task<IActionResult> MarkdownPage()
        {
            _logger.LogInformation("MardownPage controller");
            var basePath = hostingEnvironment.WebRootPath;
            var relativePath = HttpContext.Items["MarkdownPath_OriginalPath"] as string;
            if (relativePath == null) {
                _logger.LogError("MardownPage RelativePath empty");
                return NotFound(); 
            }
                

            relativePath = NormalizePath(relativePath).Substring(1);
            var pageFile = Path.Combine(basePath,relativePath);
            _logger.LogInformation($"MardownPage Pagefile: {pageFile}");
            if (!System.IO.File.Exists(pageFile))
            {
                _logger.LogError($"MardownPage Finner ikke fil: {pageFile}");
                return NotFound();
            }

            var markdown = await System.IO.File.ReadAllTextAsync(pageFile);
            if (string.IsNullOrEmpty(markdown))
                return NotFound();
            var Modelobj = new MarkdownModel();
            Modelobj.Title = "MarkdownPage";
            Modelobj.RenderedMarkdown = Markdown.ParseHtmlString(markdown);
            _logger.LogInformation($"MardownRender: {Modelobj.RenderedMarkdown.ToString().Substring(0,100)+"..."}");
            //            ViewBag.MarkdownText = Markdown.ParseHtmlString(markdown);
            return View("MarkdownPage", Modelobj);
        }

        /// <summary>
        /// Normalizes a file path to the operating system default
        /// slashes.
        /// </summary>
        /// <param name="path"></param>
        static string NormalizePath(string path)
        {
            //return Path.GetFullPath(path); // this always turns into a full OS path

            if (string.IsNullOrEmpty(path))
                return path;

            char slash = Path.DirectorySeparatorChar;
            path = path.Replace('/', slash).Replace('\\', slash);
            return path.Replace(slash.ToString() + slash.ToString(), slash.ToString());
        }
    }
}
