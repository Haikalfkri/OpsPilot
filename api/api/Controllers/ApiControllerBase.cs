using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace OpsPilot.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Produces("application/json")]
    public abstract class ApiControllerBase : ControllerBase
    {
    }
}
