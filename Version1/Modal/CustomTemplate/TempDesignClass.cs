using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SQCScanner.Modal.CustomTemplate
{
    public class TempDesignClass
    {
        public class StaticImgDesign
        {
            [Key]
            public int Id { get; set; }

            [Required]
            public string? TemplateName { get; set; }
            public string? TemplateId { get; set; }
            public string? Discription { get; set; }
            public string? TemplateJSON { get; set; }
            public string? TemplateImage { get; set; }

            [NotMapped]
            public IFormFile? TemplateJsonFile { get; set; }
            public IFormFile? TemplateLogo { get; set; }
        }
    }
}
