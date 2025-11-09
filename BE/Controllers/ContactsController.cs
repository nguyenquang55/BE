using Application.Abstractions.Services;
using Application.Contracts.Contact;
using BE.Controllers.Common;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BE.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactsController : BaseController
    {
        private readonly IContactService _contacts;
        private readonly ISessionService _sessions;

        public ContactsController(IContactService contacts, ISessionService sessions)
        {
            _contacts = contacts;
            _sessions = sessions;
        }

        private async Task<Guid?> ResolveUserIdAsync(string? sessionToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) return null;
            var res = await _sessions.GetSessionByTokenAsync(sessionToken, ct);
            return res.Success ? res.Data!.UserId : (Guid?)null;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? sessionToken, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var uid = await ResolveUserIdAsync(sessionToken ?? Request.Query["sessionToken"], ct);
            if (uid == null) return Unauthorized();
            return await HandleAsync(_contacts.ListAsync(uid.Value, search, page, pageSize, ct));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get([FromRoute] Guid id, [FromQuery] string? sessionToken, CancellationToken ct = default)
        {
            var uid = await ResolveUserIdAsync(sessionToken ?? Request.Query["sessionToken"], ct);
            if (uid == null) return Unauthorized();
            return await HandleAsync(_contacts.GetAsync(uid.Value, id, ct));
        }

        //[HttpPost]
        //public async Task<IActionResult> Create([FromBody] CreateContactRequest request, [FromQuery] string? sessionToken, CancellationToken ct = default)
        //{
        //    var uid = await ResolveUserIdAsync(sessionToken ?? Request.Query["sessionToken"], ct);
        //    if (uid == null) return Unauthorized();
        //    return await HandleAsync(_contacts.CreateAsync(uid.Value, request, ct));
        //}

        [HttpPost]
        public async Task<IActionResult> BulkCreate([FromBody] CreateContactRequest[] requests, [FromQuery] string? sessionToken, CancellationToken ct = default)
        {
            var uid = await ResolveUserIdAsync(sessionToken ?? Request.Query["sessionToken"], ct);
            if (uid == null) return Unauthorized();
            return await HandleAsync(_contacts.CreateManyAsync(uid.Value, requests, ct));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateContactRequest request, [FromQuery] string? sessionToken, CancellationToken ct = default)
        {
            var uid = await ResolveUserIdAsync(sessionToken ?? Request.Query["sessionToken"], ct);
            if (uid == null) return Unauthorized();
            return await HandleAsync(_contacts.UpdateAsync(uid.Value, id, request, ct));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete([FromRoute] Guid id, [FromQuery] string? sessionToken, CancellationToken ct = default)
        {
            var uid = await ResolveUserIdAsync(sessionToken ?? Request.Query["sessionToken"], ct);
            if (uid == null) return Unauthorized();
            return await HandleAsync(_contacts.DeleteAsync(uid.Value, id, ct));
        }
    }
}
