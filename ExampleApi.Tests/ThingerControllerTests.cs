using System;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using ExampleApi.Controllers;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mongo2Go;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using Verify.MongoDB;
using VerifyTests.EntityFramework;
using static ExampleApi.Controllers.ThingerController;
using static MassTransit.ValidationResultExtensions;

namespace ExampleApi.Tests;

[TestFixture]
public class ThingerControllerTests
{
    // Things to test:
    // - HTTP request / response (done)
    // - Mongo (done, should test insert / update / deletes)
    // - Logging (done)
    // - Entity Framework (done, should test in-memory / insert / update / deletes)
    // - Mass Transit

    private WebApplicationFactory<Program> _factory;
    private LoggerProvider _loggingProvider;
    private MongoDbRunner _mongoDbRunner;
    private ThingerContext _thingerContext;
    private SqliteConnection _thingerConnection;

    private HttpClient _apiClient;

    [ModuleInitializer]
    public static void Initialize()
    {
        VerifierSettings.IgnoreMember("ScopeId");

        VerifyHttp.Enable();
        VerifierSettings.IgnoreMember("traceId");   // None 200 responses, include a trace id it's a bit useless
        //VerifierSettings.IgnoreMember("Request");   // Ignore the request on the HttpResponse


        VerifyMongoDb.Enable();
        ////VerifierSettings.IgnoreMember("_id");
        VerifierSettings.IgnoreMember("RequestId");
        VerifierSettings.IgnoreMember("OperationId");       
        VerifierSettings.IgnoreMember("ConnectionId"); // Connection details are not very useful
        VerifierSettings.IgnoreMember("StartTime"); // StartTime will always be different
        VerifierSettings.IgnoreMember("Duration"); // Duration is too variable to be useful

        VerifyMicrosoftLogging.Enable();
        VerifierSettings.IgnoreMember("State"); // Gives us the args for log command, half useful but lots of data

        VerifyEntityFramework.Enable();

        //VerifyMassTransit.Enable();
    }

    [SetUp]
    public void Setup()
    {
        // Everything used to go a bit funny if this was async, the PerserveExecutionContext *might* have solved this...

        _mongoDbRunner = MongoDbRunner.Start();

        var mongoSettings = MongoClientSettings.FromConnectionString(_mongoDbRunner.ConnectionString);
        mongoSettings.EnableRecording();

        var mongoClient = new MongoClient(mongoSettings);
        var mongoDatabase = mongoClient.GetDatabase("TestDatabase");

        mongoDatabase
            .GetCollection<Thinger>("Thingers")
            .InsertOne(new Thinger { Name = "TestName", Content = "OldContent" });

        _loggingProvider = LoggerRecording.Start();

        _thingerConnection = new SqliteConnection("Filename=:memory:");
        _thingerConnection.Open();

        _thingerContext = new ThingerContext(
            new DbContextOptionsBuilder<ThingerContext>()
                .UseSqlite(_thingerConnection)
                .EnableRecording()
                .Options);

        _thingerContext.Database.EnsureCreated();
        _thingerContext.Thingers.Add(new Thinger { Name = "TestName", Content = "OldContent" });
        _thingerContext.SaveChanges();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(s =>
                {
                    s.AddSingleton(_ => _loggingProvider.CreateLogger<ThingerController>());
                    s.AddSingleton(_ => mongoDatabase);
                    s.AddSingleton(_ => _thingerContext);
                    //s.AddMassTransitInMemoryTestHarness(x =>
                    //{
                    //    x.AddRider(r =>
                    //    {
                    //        r.AddProducer<Thinger>("topic-thinger");
                    //    });
                    //});
                });
            });

        _factory.Server.PreserveExecutionContext = true; // This is required because the mongo recorder uses an async local to collect data
        _apiClient = _factory.CreateClient();

        MongoDBRecording.StartRecording();
        EfRecording.StartRecording();
    }

    [TearDown]
    public void TearDown()
    {
        _factory?.Dispose();
        _loggingProvider?.Dispose();
        _mongoDbRunner?.Dispose();
        _apiClient?.Dispose();
        _thingerContext?.Dispose();
        _thingerConnection?.Dispose();
    }

    [Test]
    [TestCase("TestName")]
    [TestCase("MissingName")]
    public async Task Get(string name)
    {
        var requ = new HttpRequestMessage
        { 
            RequestUri = new Uri($"/thinger?name={name}", UriKind.Relative),
            Method = HttpMethod.Post,
            Content = new StringContent("{ 'thinger': 'test' }", System.Text.Encoding.UTF8, "application/json")
        };

        var thingy = Newtonsoft.Json.JsonConvert.SerializeObject(requ);
        var tjomg = System.Text.Json.JsonSerializer.Serialize(requ);

        var req = new HttpRequestMessage
        {
            RequestUri = new Uri($"/thinger?name={name}", UriKind.Relative),
            Method = HttpMethod.Get
        };

        var a = await _apiClient.SendAsync(req);
        await Verifier.Verify(a);

        //var result = await _apiClient.GetAsync($"/thinger?name={name}");
        //await Verifier.Verify(result);
    }

    //[Test]
    //public async Task Delete()
    //{
    //    using var client = _factory.CreateClient();

    //    var result = await client.DeleteAsync("/thinger?name=TestName");

    //    await Verify(result);
    //    await Verify(_thingerCollection);
    //}

    //[Test, TestCaseSource(nameof(InputFiles), methodParams: new object[] { "TestControllerTests.SimplePostTest.*.request.json" })]
    //public async Task Put(string content)
    //{
    //    using var client = _factory.CreateClient();

    //    var result = await client.PutAsync("/thinger", new StringContent(content, System.Text.Encoding.UTF8, "application/json"));

    //    //await Verify(result).UseFileName($"{GetType().Name}.{TestContext.CurrentContext.Test.Name.Replace("(", ".").Replace(")",".")}http");
    //    await Verify(result);
    //    await Verify(_thingerCollection);
    //}

    public static IEnumerable<TestCaseData> InputFiles(string pattern)
    {
        var a = 1;
        yield return new TestCaseData("{ \"name\": \"TestName\", \"content\": \"NewContent\" }").SetArgDisplayNames(new[] { "Hello" });
    }
}