using System.Collections.Generic;

namespace MyDigitalLibrary.Core.Services
{
    public class ImportResult
    {
        public int Scanned { get; set; }
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }
}
