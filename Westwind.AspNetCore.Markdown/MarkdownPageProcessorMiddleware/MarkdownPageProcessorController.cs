using System;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Westwind.AspNetCore.Markdown.Utilities;
namespace Westwind.AspNetCore.Markdown
{
    ///
    /// Håndtering Westwind implementeringen av md-fil kan refrereres uten .md (extension) er endret.
    /// Nå vil det letes etter en tilhørende mappe og subnivå for et nytt sett med md filer. Det lages en overskrift med lenke til sub-mappen.
    /// Hvis mappe inneholder en fil med navn .order vil filsett og rekkefølge i .order brukes ellers brukes alle filene med .md extension i alfabetisk rekkefølge. Innhold i md-fil med en eventuell mappeoverskrift (og lenke), legges til siden som vises.
    ///

    /// <summary>
    /// A generic controller implementation for processing Markdown
    /// files directly as HTML content
    /// </summary>
    [ApiExplorerSettings(IgnoreApi=true)]
    public class MarkdownPageProcessorController : Controller
    {
        public MarkdownConfiguration MarkdownProcessorConfig { get; }

        private readonly IWebHostEnvironment hostingEnvironment;
        private readonly ILogger _logger;

        public MarkdownPageProcessorController(
            IWebHostEnvironment hostingEnvironment,
            MarkdownConfiguration config,
            ILogger<MarkdownPageProcessorController> logger)
        {
            _logger = logger;
            MarkdownProcessorConfig = config;
            this.hostingEnvironment = hostingEnvironment;
        }

        [HttpGet]
        [Route("markdownprocessor/markdownpage")]		
        public async Task<IActionResult> MarkdownPage()
        {
            // Model saved in middleware processing
            var model = HttpContext.Items["MarkdownProcessor_Model"] as MarkdownModel;
            if (model == null)
                throw new InvalidOperationException(
                    "This controller is not accessible directly unless the Markdown Model is set");

            var basePath = hostingEnvironment.WebRootPath;
            var relativePath = model.RelativePath;
            if (relativePath == null) {
                _logger.LogError($"basePath: {basePath} RelativePath fra Model = null");
                return NotFound();
            }
            _logger.LogInformation($"WebRootPath: {basePath} RelativePath fra Model: {relativePath} Model PhysicalPath: {model.PhysicalPath}");


            string markdown, md;
            string WikiPathFull = Path.GetDirectoryName(model.PhysicalPath);
            string[] parts = model.RelativePath.Split('/');
            string[] partFirst = parts.Take(parts.Count() - 1).ToArray();
            
            string WikiPathRelative = Path.Combine(parts.Take(parts.Count() - 1).ToArray()).Replace("\\","/");
            _logger.LogInformation($"WikiPathFull: {WikiPathFull} RelativeWikiPath: {relativePath}");

            Encoding LocalEncoding = Encoding.UTF8;
            System.Collections.Generic.IEnumerable<string> filePaths;
            if (System.IO.Directory.Exists(WikiPathFull) ) //model.PhysicalPath))
            {
                // Directory med markdown filer og .order
                if (System.IO.File.Exists(WikiPathFull + "/" + ".order"))
                {
                    //filePaths = new List<string>();
                    filePaths = System.IO.File.ReadLines(WikiPathFull + "/" + ".order")
                    .Select(line => line.ToString()) //double.Parse(line))
                    .ToList();

                    //foreach (string item in filePaths)
                    //{
                    //    Console.WriteLine(item);
                    //}
                }
                else
                {
                    filePaths = System.IO.Directory.EnumerateFiles(WikiPathFull, "*.md");
                }
                using MemoryStream stream = new();
                using StreamWriter writer = new StreamWriter(stream);
                var enumeratedCount = 0;
                Boolean mappefinnes = false;
                foreach (var filePath in filePaths)
                {
                    string filUtenExt = Path.GetFileNameWithoutExtension(filePath);
                    string dirPath = Path.Combine(WikiPathFull, filUtenExt);
                    mappefinnes = System.IO.Directory.Exists(dirPath);
                    if (mappefinnes)
                    {

                        writer.WriteLine($"# [{Path.GetFileNameWithoutExtension(filePath).Replace("-", " ")}]({Path.Combine(filUtenExt,"Readme").Replace("\\", "/")})");
                        //Console.WriteLine($"[{Path.GetFileNameWithoutExtension(filePath).Replace("-"," ")}]({dirPath})");
                    }
//                    Console.WriteLine($"Enumerated:   {Path.GetFileName(filePath)}");
                    using (var fs = new FileStream(WikiPathFull + "\\" + filUtenExt + ".md",
                        FileMode.Open,
                        FileAccess.Read))
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        md = await sr.ReadToEndAsync();
                        await writer.WriteLineAsync(md);
                        //            Console.WriteLine(markdown);
                        // avslutt med 
                        if (mappefinnes && md.Length > 0) await writer.WriteLineAsync($"[Mer..]({ Path.Combine(filUtenExt, "Readme").Replace("\\", "/")})");// "<i class=\"fa fa-angle-right\"></i>");
                    }
                    await Task.Delay(100);
                    enumeratedCount++;
                }
                writer.Flush();
                stream.Position = 0;
                markdown = LocalEncoding.GetString(stream.ToArray());
            }
            else
            {
                if (!System.IO.File.Exists(model.PhysicalPath))
                {
                    _logger.LogError($"Finner ikke model.PhysicalPath: {model.PhysicalPath}");
                    return NotFound();
                }

                // string markdown = await File.ReadAllTextAsync(pageFile);
                using (var fs = new FileStream(model.PhysicalPath,
                    FileMode.Open,
                    FileAccess.Read))
                using (StreamReader sr = new StreamReader(fs))
                {
                    markdown = await sr.ReadToEndAsync();
                }
            }

            if (string.IsNullOrEmpty(markdown))
                return NotFound();

            // set title, raw markdown, yamlheader and rendered markdown
            ParseMarkdownToModel(markdown, model);

            if (model.FolderConfiguration != null)
            {
                model.FolderConfiguration.PreProcess?.Invoke(model, this);
                return View(model.FolderConfiguration.ViewTemplate, model);
            }

            return View(MarkdownConfiguration.DefaultMarkdownViewTemplate, model);
        }

        private MarkdownModel ParseMarkdownToModel(string markdown, MarkdownModel model = null)
        {
            if (model == null)
                model = new MarkdownModel();

            string yaml = null;
            string firstLinesText = null;
            var firstLines = StringUtils.GetLines(markdown, 50).ToList();

            if (markdown.StartsWith("---"))
            {
                firstLinesText = String.Join("\n", firstLines);
                yaml = StringUtils.ExtractString(firstLinesText, "---", "---", returnDelimiters: true);
            }

            if (yaml != null && yaml.Contains("basePath: "))
                model.BasePath = StringUtils.ExtractString(yaml, "basePath: ", "\n")?.Trim();
            if (string.IsNullOrEmpty(model.BasePath))
                model.BasePath = model.FolderConfiguration.BasePath;

            if (model.FolderConfiguration.ExtractTitle )
            {
                if (yaml != null)
                {
                    model.Title = StringUtils.ExtractString(yaml, "title: ", "\n");
                    model.YamlHeader = yaml.Replace("---", "").Trim();
                }

                // if we don't have Yaml headers the header has to be closer to the top
                firstLines = firstLines.Take(10).ToList();
                
                if (string.IsNullOrEmpty(model.Title))
                {
                    foreach (var line in firstLines)
                    {
                        if (line.TrimStart().StartsWith("# "))
                        {
                            model.Title = line.TrimStart(' ', '\t', '#');
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(model.Title))
                {
                    for (var index = 0; index < firstLines.Count; index++)
                    {                    
                        var line = firstLines[index];
                        if (line.TrimStart().StartsWith("===") && index > 0)
                        {
                            // grab the previous line
                            model.Title = firstLines[index-1].Trim();
                            break;
                        }
                    }
                }
            }

            model.RawMarkdown = markdown;
            model.RenderedMarkdown = Markdown.ParseHtmlString(markdown, sanitizeHtml:model.FolderConfiguration.SanitizeHtml);

            return model;
        }
    }
}
