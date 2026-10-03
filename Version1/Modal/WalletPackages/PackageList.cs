using System.ComponentModel.DataAnnotations;

namespace SQCScanner.Modal.Wallet
{
    public class PackageList
    {
        [Key]
        public int PackId { get; set; }
        public string ButtlePints { get; set; }
    }
}
