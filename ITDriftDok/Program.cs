using Markdig.Extensions.AutoIdentifiers;
using Markdig;
using Westwind.AspNetCore.Markdown;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Log4Net.AspNetCore;
using System.Configuration;
using Microsoft.Extensions.FileProviders; // multiple content wwwroot + wiki

ILogger? logger;
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddLog4Net();

var loggerFactory = builder.Services.BuildServiceProvider().GetService<ILoggerFactory>();
logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("CreateLogger");

string appsettingsFile = "appsettings.json";
string wikiFoldersFile = "WikiFolders.json";
string appsettingsFilePath = Directory.GetCurrentDirectory() + @"\" + appsettingsFile;
string wikiFoldersFilePath = Directory.GetCurrentDirectory() + @"\" + wikiFoldersFile;
// Får ikke distrubuert kun med mal-filer, uten appsetting.json og WikiFolders.json. Er derfor overflødig. def rutine definisjon
// if (!SjekkSettingsFiler(appsettingsFile, wikiFoldersFile, logger)) { logger.LogError("Settings kopiering feilet"); }

bool appsettingsExist = File.Exists(appsettingsFilePath);
bool wikiFoldersExist = File.Exists(wikiFoldersFilePath);
if (!appsettingsExist || !wikiFoldersExist)
{
    // sjekk om _appsettings og _wikiFolders existerer
    //    File.Copy(appsettingsFile, appsettingsFile.Replace("_",""));
    if (!appsettingsExist) {
        appsettingsExist = File.Exists(appsettingsFile);
        logger.LogError("appsettings.json mangler (Bruk _appsettings.json som mal"); }
    if (!wikiFoldersExist) { logger.LogError("wikiFolders.json mangler (Bruk _wikiFolders.json som mal"); }
    throw new Exception("Settingsfil(er) mangler sjekkloggfil");
}
// Add services to the container.

var configbuilder = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddJsonFile("WikiFolders.json");

var processingFolders = configbuilder.Build().GetSection("RootFolder").Get<Processing[]>();
//var wikiFolders = await LesInnhold();
var wikiFolders = configbuilder.Build().GetSection("WikiFolders").Get<WikiFolder[]>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddMarkdown(config =>
{
    //    config.AddMarkdownProcessingFolder("/wiki/", "~/Pages/__MarkdownPageTemplate.cshtml");
    // optional Tag BlackList
    config.HtmlTagBlackList = "script|iframe|object|embed|form"; // default

    //// Simplest: Use all default settings
    var folderConfig = config.AddMarkdownProcessingFolder("/docs/", "~/Pages/__MarkdownPageTemplate.cshtml");
    foreach (var process in processingFolders)
    {

        //// Customized Configuration: Set FolderConfiguration options
        //folderConfig = config.AddMarkdownProcessingFolder("/wiki/", "~/Pages/__MarkdownPageTemplate.cshtml");
        //folderConfig = config.AddMarkdownProcessingFolder("/posts/", "~/Pages/__MarkdownPageTemplate.cshtml");
        folderConfig = config.AddMarkdownProcessingFolder(process.Folder, process.MalSide);
    };

    // Optionally strip script/iframe/form/object/embed tags ++
    folderConfig.SanitizeHtml = false;  //  default

    // Optional configuration settings
    folderConfig.ProcessExtensionlessUrls = true;  // default
    folderConfig.ProcessMdFiles = true; // default

    // Optional pre-processing - with filled model
    folderConfig.PreProcess = (model, controller) =>
    {
        // controller.ViewBag.Model = new MyCustomModel();
    };

    // folderConfig.BasePath = "https://github.com/RickStrahl/Westwind.AspNetCore.Markdow/raw/master";

    // Create your own IMarkdownParserFactory and IMarkdownParser implementation
    // to replace the default Markdown Processing
    //config.MarkdownParserFactory = new CustomMarkdownParserFactory();                 

    // optional custom MarkdigPipeline (using MarkDig; for extension methods)
    config.ConfigureMarkdigPipeline = builder =>
    {
        builder.UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Default)
            .UsePipeTables()
            .UseGridTables()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub) // Headers get id="name" 
            .UseAutoLinks() // URLs are parsed into anchors
            .UseAbbreviations()
            .UseYamlFrontMatter()
            .UseEmojiAndSmiley(true)
            .UseListExtras()
            .UseFigures()
            .UseTaskLists()
            .UseCustomContainers()
            //.DisableHtml()   // renders HTML tags as text including script
            .UseGenericAttributes();
    };
});
// We need to use MVC so we can use a Razor Configuration Template
// for the Markdown Processing Middleware
builder.Services.AddMvc()
    // have to let MVC know we have a controller otherwise it won't be found
    .AddApplicationPart(typeof(MarkdownPageProcessorMiddleware).Assembly);
var serviceProvider = builder.Services.BuildServiceProvider();
var markdownlogger = serviceProvider.GetService<ILogger<ITDriftDok.Controllers.MarkdownController>>();
builder.Services.AddSingleton(typeof(ILogger), markdownlogger);
builder.Services.AddSingleton(typeof(WikiFolder[]), wikiFolders);


var app = builder.Build();
//var loggerFactory = app.Services.GetService<ILoggerFactory>();
//logger = loggerFactory.CreateLogger<Program>();
//logger.LogInformation("CreateLogger");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/500");
    //app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseDefaultFiles(new DefaultFilesOptions()
{
    DefaultFileNames = new List<string> { "index.md", "index.cshtml" }
});
// Ultra-simplistic Markdown router
logger.LogInformation("Markdown router");

app.Use(async (context, next) =>
{
    logger.LogInformation(context.Request.Path.Value);
    //Endpoint? currentEndpoint = context.GetEndpoint();
    //if (currentEndpoint is null)
    //{
    //    logger.LogInformation("ITDriftDok - CurrentEndPoint = null");
    //    await next();
    //    return;
    //}
    //logger.LogInformation($"ITDriftDok - Endpoint: {currentEndpoint.DisplayName}");

    //if (currentEndpoint is RouteEndpoint routeEndpoint)
    //{
    //    logger.LogInformation($"ITDriftDok - - Route Pattern: {routeEndpoint.RoutePattern}");
    //}
    //foreach (var endpointMetadata in currentEndpoint.Metadata)
    //{
    //    logger.LogInformation($"ITDriftDok - - Metadata: {endpointMetadata}");
    //}

    if (context.Request.Path.Value.EndsWith(".md", StringComparison.InvariantCultureIgnoreCase))
    {

        context.Items["MarkdownPath_OriginalPath"] = context.Request.Path.Value;
        // rewrite path to our controller so we can use _layout page
        context.Request.Path = "/markdown/markdownpage";
    }

    await next();
});
/// Start multiprovider/compositProvider
/// Multiple content folder wwwroot og wikis
///  må endre i MarkdownPageProcessorMiddleware  WebRoot mulig søke hint: var base2 = _env.WebRootFileProvider.GetDirectoryContents("wikis");
/// må "/wikis/" alternativ provider virker ikke her. Denne bruker fortsatt basePath .../wwwroot/wikis/
/// 
// Build the different providers you need
//var webRootProvider = new PhysicalFileProvider(builder.Environment.WebRootPath);
//var newPathProvider = new PhysicalFileProvider(
//  Path.Combine(builder.Environment.ContentRootPath, @"wikis"));

//// Create the Composite Provider
//var compositeProvider = new CompositeFileProvider(webRootProvider,
//                                                  newPathProvider);

//// Replace the default provider with the new one
//app.Environment.WebRootFileProvider = compositeProvider;
//// Slutt multiprovider

app.UseMarkdown();
  logger.LogInformation("Use Markdown set");
app.UseRouting();

app.UseStaticFiles();

///// Multiple content folder wwwroot og wikis
//app.UseStaticFiles(new StaticFileOptions()
//{
//    // Add the other folder, using the Content Root as the base
//    FileProvider = new PhysicalFileProvider(
//    Path.Combine(builder.Environment.ContentRootPath, "wikis"))
//});

app.UseAuthorization();

app.MapRazorPages();
  logger.LogInformation("RazorPages set");
app.MapControllers();
//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Home}/{action=Index}/{id?}");

logger.LogInformation("Run");
app.Run();
logger.LogInformation("Run avsluttet");

//// Får ikke distrubuert kun med mal-filer, uten appsetting.json og WikiFolders.json. Er derfor overflødig
//static bool SjekkSettingsFiler(string AppsettingsFile, string WikiFoldersFile, ILogger? logger)
//{
//    string appsettingsFilePath = Directory.GetCurrentDirectory() + @"\" + AppsettingsFile;
//    string wikiFoldersFilePath = Directory.GetCurrentDirectory() + @"\" + WikiFoldersFile;

//    bool appsettingsExist = File.Exists(appsettingsFilePath);
//    bool wikiFoldersExist = File.Exists(wikiFoldersFilePath);
//    bool kopiertOK = true;
//    if (!appsettingsExist || !wikiFoldersExist)
//    {
//        // sjekk om _appsettings og _wikiFolders existerer
//        //    File.Copy(appsettingsFile, appsettingsFile.Replace("_",""));
//        kopiertOK = false;
//        if (!appsettingsExist)
//        {
//            string appsettingsMalFilePath = Directory.GetCurrentDirectory() + @"\_" + AppsettingsFile;
//            if (File.Exists(appsettingsMalFilePath))
//            {
//                try
//                {
//                    File.Copy(appsettingsMalFilePath, appsettingsFilePath, true);
//                    kopiertOK = true;
//                }
//                catch (IOException iox)
//                {
//                    logger.LogError($"Feil ved kopiering av _appsettings.json malfil({iox.Message})");
//                }
//            }
//            else
//            {
//                logger.LogError("_appsettings.json malfil mangler");
//            }
//        }
//        if (!wikiFoldersExist)
//        {
//            string wikiFoldersMalFilePath = Directory.GetCurrentDirectory() + @"\_" + WikiFoldersFile;
//            if (File.Exists(wikiFoldersMalFilePath))
//            {
//                try
//                {
//                    File.Copy(wikiFoldersMalFilePath, wikiFoldersFilePath, true);
//                    // kopiertOK ikke OK hvis feilet på første fil
//                }
//                catch (IOException iox)
//                {
//                    kopiertOK = false;
//                    logger.LogError($"Feil ved kopiering av _WikiFolders.json malfil({iox.Message})");
//                }
//            }
//            else
//            {
//                logger.LogError("_WikiFolders.json malfil mangler");
//            }
//        }
//    }

//    return kopiertOK;
//}
/// <summary>
/// Wikifolder Innhold i et liste array
/// Bruker config-section istedenfor i addsingleton(typeof(WikiFolders), ...) 
/// </summary>
//static async Task<List<WikiFolder>>? LesInnhold() {
//    string fileName = "WikiFolders.json";
//    var options = new JsonSerializerOptions
//    {
//        ReadCommentHandling = JsonCommentHandling.Skip
//    };
//    using FileStream openStream = File.OpenRead(fileName);
//    var wikiFolders =
//        JsonSerializer.DeserializeAsync<List<WikiFolder>>(openStream, options);

//    //Console.WriteLine($"Date: {WikiFolder?.Date}");
//    //Console.WriteLine($"TemperatureCelsius: {WikiFolder?.TemperatureCelsius}");
//    //Console.WriteLine($"Summary: {WikiFolder?.Summary}");

//    return await wikiFolders;
//}
public class Processing
{
    public string? Folder { get; set; }
    public string? MalSide { get; set; }
}
public class WikiFolder
{
    public string? Gruppe { get; set; }
    public string? Folder { get; set; }
    public string? Tittel { get; set; }
    public string? Beskrivelse { get; set; }

    //public static implicit operator List<object>(WikiFolder? v)
    //{
    //    //throw new NotImplementedException();
    //    return v == null ? new List<object>() : v;
    //}
}