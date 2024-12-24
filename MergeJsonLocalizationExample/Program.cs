
using Microsoft.AspNetCore.Localization;
using My.Extensions.Localization.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Reflection;
using JsonLocalizationExample;
using Microsoft.Extensions.Localization;

namespace MergeJsonLocalizationExample;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        MergeJsonFiles(); // ! Call it before AddJsonLocalization and UseLocalization

        builder.Services.AddJsonLocalization(delegate(JsonLocalizationOptions options)
        {
            options.ResourcesPath = "Resources";
        });


        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthorization();

        app.UseLocalization(builder.Configuration);

        app.MapControllers();

        app.MapGet("/debug-localization", (IStringLocalizer localizer) =>
        {
            var strings = localizer.GetAllStrings().ToDictionary(s => s.Name, s => s.Value);
            return Results.Json(strings);
        });

        app.Run();
    }

    private static void MergeJsonFiles()
    {
        var nugetAssembly = typeof(ClassForAssembly).Assembly;
        var embeddedJson = LoadEmbeddedJson(nugetAssembly, "en-US.json");
        var consumerJson = LoadJsonFile("Resources/en-US.json");

        var mergedJson = MergeJson(embeddedJson, consumerJson);

        WriteJsonToFile(mergedJson, "Resources/en-US.json");
    }

    private static void UseLocalization(this IApplicationBuilder app, IConfiguration configuration)
    {
        var source = configuration.GetSection("Localization:SupportedCultures").Get<List<string>>() ?? new List<string>();
        var text = configuration.GetValue<string>("Localization:DefaultCulture") ?? "en-US";
        var list = source.Select((string s) => new CultureInfo(s)).ToList();
        list.Add(new CultureInfo(text));
        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(text),
            SupportedCultures = list,
            SupportedUICultures = list
        };
        app.UseRequestLocalization(options);
    }

    private static JObject LoadEmbeddedJson(Assembly assembly, string resourceName)
    {
        var name = assembly.GetManifestResourceNames().First((string r) => r.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream == null)
        {
            throw new FileNotFoundException("Embedded resource '" + resourceName + "' not found.");
        }

        using var streamReader = new StreamReader(stream);
        var json = streamReader.ReadToEnd();
        return JObject.Parse(json);
    }

    private static JObject LoadJsonFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File '" + filePath + "' not found.");
        }

        string json = File.ReadAllText(filePath);
        return JObject.Parse(json);
    }

    private static JObject MergeJson(JObject embeddedJson, JObject consumerJson)
    {
        embeddedJson.Merge(consumerJson, new JsonMergeSettings
        {
            MergeArrayHandling = MergeArrayHandling.Union,
            MergeNullValueHandling = MergeNullValueHandling.Merge
        });
        return embeddedJson;
    }

    private static void WriteJsonToFile(JObject json, string filePath)
    {
        File.WriteAllText(filePath, json.ToString());
    }
}
