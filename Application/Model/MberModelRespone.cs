using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Model
{
    public class MberModelRespone
    {
        public string? InputText { get; set; }
        public string? Intent { get; set; }
        public double ConfidenceScore { get; set; }
    }
}
