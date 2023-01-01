using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MassTransit;

namespace ExampleApi.Controllers;

[ApiController]
[Route("[controller]")]
public class ThingerController : ControllerBase
{
    private readonly ILogger<ThingerController> _logger;
    private readonly IMongoCollection<Thinger> _thingerCollection;
    private readonly ThingerContext _thingerContext;
    //private readonly ITopicProducer<Thinger> _thingerProducer;

    public ThingerController(
        IMongoCollection<Thinger> thingerCollection,
        ThingerContext thingerContext,
        ILogger<ThingerController> logger)//,
        //ITopicProducer<Thinger> thingerProducer)
    {
        _thingerCollection = thingerCollection ?? throw new ArgumentNullException(nameof(thingerCollection));
        _thingerContext = thingerContext ?? throw new ArgumentNullException(nameof(thingerContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        //_thingerProducer = thingerProducer ?? throw new ArgumentNullException(nameof(thingerProducer));
    }

    [HttpGet(Name = "GetThinger")]
    public async Task<IActionResult> GetAsync([FromQuery] string name, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Recieved request to find thinger with '{Name}'", name);

        var mongoResult = await _thingerCollection
            .AsQueryable()
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

        var efResult = await _thingerContext.Thingers
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

        var result = mongoResult ?? efResult;

        //await _thingerProducer.Produce(result, cancellationToken);

        if (result is null)
        {
            _logger.LogInformation("Did not find thinger with '{Name}'", name);
            return new NotFoundResult();
        }
        else
        {
            _logger.LogInformation("Found thinger with '{Name}'", name);
            return new OkObjectResult(result);
        }
    }

    [HttpPut(Name = "UpsertThinger")]
    public async Task<IActionResult> PutAsync([FromBody] Thinger request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Recieved request to upsert thinger with '{Name}'", request.Name);

        var mongoResult = await _thingerCollection.FindOneAndReplaceAsync(
            e => e.Name == request.Name,
            request,
            new() { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            cancellationToken);

        var efEntity = await _thingerContext.Thingers
            .FirstOrDefaultAsync(x => x.Name == request.Name, cancellationToken);

        if (efEntity is null)
        {
            efEntity = new Thinger { Name = request.Name, Content = request.Content };
            await _thingerContext.Thingers.AddAsync(efEntity, cancellationToken);
        }
        else
        {
            efEntity.Content = request.Content;
        }

        await _thingerContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(mongoResult);
    }

    [HttpDelete(Name = "DeleteThinger")]
    public async Task<IActionResult> DeleteAsync([FromQuery] string name, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Recieved request to delete thinger with '{Name}'", name);

        var mongoResult = await _thingerCollection
            .DeleteOneAsync(e => e.Name == name, cancellationToken);

        var efEntity = await _thingerContext.Thingers
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

        _thingerContext.Thingers.Remove(efEntity);
        await _thingerContext.SaveChangesAsync(cancellationToken);

        if (mongoResult.DeletedCount == 0)
        {
            _logger.LogInformation("Did not find thinger with '{Name}'", name);
            return new NotFoundResult();
        }
        else
        {
            _logger.LogInformation("Deleted thinger with '{Name}'", name);
            return new NoContentResult();
        }
    }

    public class ThingerContext : DbContext 
    {
        public DbSet<Thinger> Thingers { get; init; }

        public ThingerContext(DbContextOptions options) : base(options) { }
    }

    [BsonIgnoreExtraElements]
    public class Thinger
    {
        [Key]
        public string Name { get; init; }
        public string Content { get; set; }
    }
}