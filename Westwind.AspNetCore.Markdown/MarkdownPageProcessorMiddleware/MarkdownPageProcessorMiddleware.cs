#region License
/*
 **************************************************************
 *  Author: Rick Strahl 
 *          © West Wind Technologies, 
 *          https://www.west-wind.com/
 * 
 * Created: 3/25/2018
 *
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 **************************************************************  
*/
#endregion

using System;
using System.Collections;
using System.ComponentModel.Design.Serialization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace Westwind.AspNetCore.Markdown
{
    /// <summary>
    /// Middleware that allows you to serve static Markdown files from disk
    /// and merge them using a configurable View template.
    /// </summary>
    public class MarkdownPageProcessorMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly MarkdownConfiguration _configuration;

        private readonly IWebHostEnvironment _env;


        public MarkdownPageProcessorMiddleware(RequestDelegate next,
            MarkdownConfiguration configuration,
            IWebHostEnvironment _env
        )
        {
            _next = next;
            _configuration = configuration;
            this._env = _env;
        }

        public Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value;
            if (string.IsNullOrEmpty(path))
                return _next(context);

            bool hasExtension = !string.IsNullOrEmpty(Path.GetExtension(path));
            bool hasMdExtension = path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);
            bool isRoot = path == "/";
            bool processAsMarkdown = false;
            var basePath = _env.WebRootPath;
            //// Todo: Før content utenfor wwwroot kan støttes må referanse til bilde og java-bibliotek fikses
            /// Det kan være utelatelse av useStaticFiles i Program.cs eller gjennomgang av hele Markdown middleware.
            //var folders = _configuration.MarkdownProcessingFolders.Select(x => x.RelativePath).ToList();
            //var rel = folders.FirstOrDefault(f=> path.StartsWith(f, StringComparison.OrdinalIgnoreCase));
            //if (!isRoot && rel != null &&  rel != "/") {
            //    basePath = physicalBasePath(path);
            //}
            //// Sjekk for alternativ WebRoot mulig søke hint: var base2 = _env.WebRootFileProvider.GetDirectoryContents("wikis");
            //// må "/wikis/" alternativ provider virker ikke her. Denne bruker fortsatt basePath .../wwwroot/wikis/
            var relativePath = path;
            relativePath = PathHelper.NormalizePath(relativePath).Substring(1).TrimEnd('\\', '/');
            var pageFile = Path.Combine(basePath, relativePath);
            // process any Markdown file that has .md extension explicitly
            foreach (var folder in _configuration.MarkdownProcessingFolders)
            {
                if (!path.StartsWith(folder.RelativePath, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                if (isRoot && folder.RelativePath != "/")
                    continue;

                if (hasMdExtension)
                {
                    processAsMarkdown = true;
                }
                else if (path.StartsWith(folder.RelativePath, StringComparison.InvariantCultureIgnoreCase) &&
                         (folder.ProcessExtensionlessUrls && !hasExtension ||
                          hasMdExtension && folder.ProcessMdFiles))
                {
                        
                    // it's a physical directory - don't convert that - only virtual files
                    if (!hasExtension && Directory.Exists(pageFile))
                        continue;

                    if (!hasExtension)
                    {
                        pageFile += ".md";
                    }

                    if (!File.Exists(pageFile) && !File.Exists(pageFile.Replace(".md",".markdown")))
                        continue;

                    processAsMarkdown = true;
                }

                if (processAsMarkdown)
                {
                    var model = new MarkdownModel
                    {
                        FolderConfiguration = folder,
                        RelativePath = path,
                        PhysicalPath = pageFile
                    };

                    // push the model into the context for controller to pick up
                    context.Items["MarkdownProcessor_Model"] = model;

                    // rewrite path to our controller so we can use _layout page
                    context.Request.Path = "/markdownprocessor/markdownpage";
                    break;
                }
            }

            return _next(context);
        }
        private string physicalBasePath(string relativePath)
        {
            IDirectoryContents DirectoryContents;
            var _fileProvider = _env.WebRootFileProvider;
            //            DirectoryContents = _fileProvider.GetDirectoryContents(string.Empty);
            //string content = Path.Combine(_environment.ContentRootPath, @"wikis");
            if(relativePath != "/") { 
                string pattern = @"(?<Katalog>.*/){0,}(?<Fil>.*)"; // @"(.*\\)(.*)\\Readme"
                string relKatalog;
                string rootKatalog;
                Match m = Regex.Match(relativePath, pattern, RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    rootKatalog = m.Groups["Katalog"].Value;
                    if (rootKatalog == "/") goto defaultWebRoot;
                    relKatalog = m.Groups["Fil"].Value;
                    //Finner ikke wwwroot/wiki/sertifikat Dokumentasjon
                    string rootPhysical = rootKatalog.Replace("/", @"\");
                    DirectoryContents = _fileProvider.GetDirectoryContents(rootPhysical);//Path.Combine(_environment.ContentRootPath, @"wikis"));
                    foreach (var item in DirectoryContents)
                    {
                        if (item.PhysicalPath.Contains(rootPhysical))
                        {
                            string itemPath = item.PhysicalPath;
                            int pathIndex = item.PhysicalPath.IndexOf(rootPhysical);
                            return item.PhysicalPath.Substring(0, item.PhysicalPath.IndexOf(rootPhysical));
                        }
                    }
                }
            }
            defaultWebRoot:
            return _env.WebRootPath;
        }
    }
}
