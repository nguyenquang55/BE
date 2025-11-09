using System.Collections.Generic;

namespace Application.Contracts.Contact
{
    public class BulkCreateContactsResponse
    {
        public List<ContactDTO> Created { get; set; } = new List<ContactDTO>();
        public List<BulkItemError> Errors { get; set; } = new List<BulkItemError>();
        public int TotalRequested { get; set; }
        public int CreatedCount { get; set; }
        public int ErrorCount { get; set; }
    }
}
