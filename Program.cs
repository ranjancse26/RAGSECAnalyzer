using Pinecone;
using Serilog;
using System.Diagnostics;
using System.Reflection;
using LangChain.Splitters.Text;
using SECAnalyzer.Services;
using SECAnalyzer.Constant;
using Serilog.Sinks.SystemConsole.Themes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SECAnalyzer.Database.ApplicationDbContext;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SECAnalyzer.Domain.Request;
using SECAnalyzer.Domain.Response;
using AutomatedWebscraper.Domain.Request;
using System.Text.RegularExpressions;

Log.Logger = new LoggerConfiguration()
        .WriteTo.Console(theme: AnsiConsoleTheme.Code)
        .CreateLogger();

var builder = new ConfigurationBuilder()
       .AddJsonFile($"appSettings.json", true, true);

IConfigurationRoot config = builder.Build();

var serviceProvider = new ServiceCollection()
    .AddDbContext<AppDbContext>(options =>
        options.UseSqlite(config[AppConstant.ConnectionString]))
    .BuildServiceProvider();

string geminiApiKey = config[AppConstant.GeminiApiKey];
int httpRequestTimeout = int.Parse(config[AppConstant.HttpRequestTimeout]);

var jinaApiKey = config[AppConstant.JinaApiKey];
var pineConeApiKey = config[AppConstant.PineconeApiKey];

string indexName = "sec-10k";
var dbContext = serviceProvider.GetService<AppDbContext>();

string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
string SecFolderPath = Path.Combine(exePath, "SECFiles");
var secFiles = Directory.GetFiles(SecFolderPath);

System.Console.WriteLine("1. Vectorize Documents");
System.Console.WriteLine("2. Query Documents");

System.Console.WriteLine("\nKeyin your option: ");
string option = Console.ReadLine();

switch (option)
{
    case "1":
        foreach (var secFile in secFiles)
        {
            var company = Path.GetFileName(secFile).ToLower();
            string content = File.ReadAllText(secFile);
            var documentVectors = await VectorizeDocument(jinaApiKey, pineConeApiKey, indexName, company, content);

            if (documentVectors.Count > 0)
            {
                foreach (var documentVector in documentVectors)
                {
                    dbContext.Documents.Add(new SECAnalyzer.Database.Models.DocumentItem
                    {
                        Id = documentVector.DocumentId,
                        Document = documentVector.Document,
                        VectorUniqueId = documentVector.VectorUniqueId
                    });
                    dbContext.SaveChanges();
                }
            }
        }
        break;
    case "2":
        var companies = new List<string>();
        var vectorUniqueIds = new List<string>();
        var documents = new List<string>();

        foreach (var secFile in secFiles)
        {
            var company = Path.GetFileName(secFile).ToLower();
            companies.Add(company);
        }

        IJinaEmbeddingService jinaEmbeddingService = new JinaEmbeddingService(jinaApiKey);
        string promptsFolderPath = Path.Combine(exePath, "Prompts");
        string question = "Google and Microsoft's Revenue";
        
        string structuredQuestionsPrompt = File.ReadAllText($"{promptsFolderPath}//StructuredQuestions.txt");
        structuredQuestionsPrompt = structuredQuestionsPrompt.Replace("{{question}}", question);

        var geminiPromptService = new GeminiPromptService(geminiApiKey,
            "https://generativelanguage.googleapis.com", "gemini-2.0-flash-exp", httpRequestTimeout);

        var geminiRequestRoot = JsonConvert.DeserializeObject<GeminiStructuredQuestionInputRoot>(structuredQuestionsPrompt);
        var geminiResponseRoot = await geminiPromptService.Execute(geminiRequestRoot);

        if (geminiResponseRoot != null &&
           geminiResponseRoot.candidates.Count > 0 &&
           geminiResponseRoot.candidates[0].content != null &&
           geminiResponseRoot.candidates[0].content.parts.Count > 0)
        {
            var structuredQuestionRoot = JsonConvert.DeserializeObject<StructuredQuestionRoot>(
                geminiResponseRoot.candidates[0].content.parts[0].text);

            if (structuredQuestionRoot != null)
            {
                foreach (var structuredQuestion in structuredQuestionRoot.Response)
                {
                    if (!companies.Any(item => item == $"{structuredQuestion.Company.ToLower()}.txt"))
                        continue;

                    var vectorData = await jinaEmbeddingService.GetEmbeddingOutputAsync(new List<string>
                    {
                        structuredQuestion.Question
                    });

                    IPineconeService pineConeService = new PineconeService(pineConeApiKey);
                    var pineConeIndex = await pineConeService.GetIndex(indexName);

                    if (pineConeIndex != null && 
                        vectorData.Data.Count > 0 && 
                        vectorData.Data[0].Embedding.Count > 0)
                    {
                        System.Console.WriteLine($"\nQuestion: {structuredQuestion.Question}");
                        var metaData = new Metadata {
                            { "company", $"{structuredQuestion.Company.ToLower()}.txt" },
                        };
                        var queryResponse = await pineConeService.Query(pineConeIndex.Host,
                            vectorData.Data[0].Embedding.ToList(), metaData);
                        if (queryResponse != null)
                        {
                            var matchesWithMetadata = queryResponse.Matches!.Where(match => match.Metadata != null).ToList();
                            System.Console.WriteLine("\n***********************************************************");

                            foreach (var scoredVector in matchesWithMetadata)
                            {
                                System.Console.WriteLine($"Id: {scoredVector.Id}");
                                System.Console.WriteLine($"Score: {scoredVector.Score}");
                                System.Console.WriteLine($"DocumentId: {scoredVector.Metadata!["documentId"]}");
                                vectorUniqueIds.Add(scoredVector.Id);
                            }

                            System.Console.WriteLine("\n***********************************************************");
                        }
                    }
                }

                // Fetch all the documents and use it as a context for answering the question
                if (vectorUniqueIds.Count > 0)
                {
                    foreach (var documentId in vectorUniqueIds)
                    {
                        var filteredDocument = dbContext.Documents.Where(document => document.VectorUniqueId == documentId).FirstOrDefault();
                        if (filteredDocument != null)
                        {
                            documents.Add(RemoveSpecialCharacters(filteredDocument.Document));
                        }
                    }

                    if(documents.Count > 0)
                    {
                        string QuestionWithContextPrompt = File.ReadAllText($"{promptsFolderPath}//QuestionWithContext.txt");
                        QuestionWithContextPrompt = QuestionWithContextPrompt.Replace("{{question}}", question);
                        QuestionWithContextPrompt = QuestionWithContextPrompt.Replace("{{context}}",
                            string.Join(".", documents));

                        var geminiFinalRequestRoot = JsonConvert.DeserializeObject<GeminiInputRoot>(QuestionWithContextPrompt);
                        geminiResponseRoot = await geminiPromptService.Execute(geminiFinalRequestRoot);

                        System.Console.WriteLine($"Question: {question}");
                        if (geminiResponseRoot != null &&
                           geminiResponseRoot.candidates.Count > 0 &&
                           geminiResponseRoot.candidates[0].content != null &&
                           geminiResponseRoot.candidates[0].content.parts.Count > 0)
                        {
                            System.Console.WriteLine(geminiResponseRoot.candidates[0].content.parts[0].text);
                        }
                        else
                        {
                            System.Console.WriteLine("Some problem with the LLM call. Please debug or troubleshoot");
                        }
                    }
                    else
                    {
                        System.Console.WriteLine("Sorry, no context available to answer");
                    }
                }
            }
        }
        break;
}

Console.WriteLine("Press any key to exit!");
Console.ReadLine();

static string RemoveSpecialCharacters(string str)
{
    return Regex.Replace(str, "[^a-zA-Z0-9_.]+", "", RegexOptions.Compiled);
}

static async Task<List<DocumentVector>> VectorizeDocument(string jinaApiKey, string pineConeApiKey,
    string indexName, string company, string content)
{
    var documentVectors = new List<DocumentVector>();

    // Recursive Chunking of Document
    var recursiveTextSplitter = new RecursiveCharacterTextSplitter();

    var documents = new Dictionary<string, string>();
    foreach (var chunk in recursiveTextSplitter.SplitText(content))
    {
        string documentId = Guid.NewGuid().ToString();
        documents.Add(documentId, chunk);
    }

    var stopWatch = new Stopwatch();
    try
    {
        int index = 0;

        IJinaEmbeddingService jinaEmbeddingService = new JinaEmbeddingService(jinaApiKey);
        foreach (var document in documents)
        {
            index++;
            System.Console.WriteLine($"Started vectorizing the document: {index}");
            stopWatch.Restart();
            var vectorDocument = await jinaEmbeddingService.GetEmbeddingOutputAsync(new List<string>
            {
                document.Value
            });
            if (vectorDocument != null &&
               vectorDocument.Data.Count > 0 &&
               vectorDocument.Data[0].Embedding != null)
            {
                documentVectors.Add(new DocumentVector
                {
                    DocumentId = document.Key,
                    Document = document.Value,
                    Vectors = vectorDocument.Data[0].Embedding.ToList()
                });
            }
            stopWatch.Stop();
            System.Console.WriteLine($"Time taken to vectorize the document:" +
                $" {stopWatch.Elapsed.TotalSeconds} seconds");
        }
    }
    catch (Exception ex)
    {
        System.Console.WriteLine(ex.Message);
    }

    System.Console.WriteLine("Vectorizing documents");
    stopWatch.Restart();

    // Vectorize
    IPineconeService pineConeService = new PineconeService(pineConeApiKey);
    var pineConeIndex = await pineConeService.GetIndex(indexName);
    if (pineConeIndex == null)
    {
        var createIndexResponse = await pineConeService.CreateIndexAsync(indexName, 1024,
            CreateIndexRequestMetric.Cosine, new ServerlessSpec
            {
                Cloud = ServerlessSpecCloud.Aws,
                Region = "us-east-1",
            });

        if (createIndexResponse != null)
        {
            string host = createIndexResponse.Host;
            foreach (var documentVector in documentVectors)
            {
                var metaData = new Metadata {
                    { "company", company },
                    { "documentId", documentVector.DocumentId }
                };
                var (upsertResponse, uniqueId) = await pineConeService.Vectorize(host,
                    documentVector.Vectors, metaData);
                if (upsertResponse != null)
                {
                    documentVector.VectorUniqueId = uniqueId;
                }
            }
        }
    }
    else
    {
        string host = pineConeIndex.Host;
        foreach (var documentVector in documentVectors)
        {
            var metaData = new Metadata {
                    { "company", company },
                    { "documentId", documentVector.DocumentId }
                };
            var (upsertResponse, uniqueId) = await pineConeService.Vectorize(host,
                documentVector.Vectors, metaData);
            if (upsertResponse != null)
            {
                documentVector.VectorUniqueId = uniqueId;
            }
        }
    }

    stopWatch.Stop();
    System.Console.WriteLine($"Total time taken to vectorize the documents:" +
                $" {stopWatch.Elapsed.TotalSeconds} seconds");
    return documentVectors;
}

public class DocumentVector
{
    public string DocumentId { get; set; }
    public string Document { get; set; }
    public string VectorUniqueId { get; set; }
    public List<float> Vectors { get; set; }
}