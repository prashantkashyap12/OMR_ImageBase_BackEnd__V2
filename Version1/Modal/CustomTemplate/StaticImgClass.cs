using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SQCScanner.Modal.CustomTemplate
{
    public class StaticImgClass
    {
        [Key]
        public int Id { get; set; }
        public string? TemplateId { get; set; }
        public string? TemplateName { get; set; }
        public string? discription { get; set; }
        public string? TemplateImgBase64 { get; set; }
        [NotMapped]
        public IFormFile? TemplateImg { get; set; }
    }
}