using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SQCScanner.Modal.CustomTemplate
{
    public class TempDesignClass
    {
        public int Id { get; set; }
        public string? TemplateLogo { get; set; }
        public string? TemplateBarcode { get; set; }
        [NotMapped]
        public IFormFile? TemplateLogoFile { get; set; }
        [NotMapped]
        public IFormFile? TemplateBarcodeFile { get; set; }
    }
}
