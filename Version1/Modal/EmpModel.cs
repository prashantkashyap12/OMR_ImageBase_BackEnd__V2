using System.ComponentModel.DataAnnotations;

namespace SQCScanner.Modal
{
    public class EmpModel
    {
        [Key]
        public int Id { get; set; } = 0;
        public string EmpName { get; set; } = string.Empty;
        public string EmpLastName { get; set; } = string.Empty;
        public string EmpEmail { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
        public string contact { get; set; } = string.Empty;
        public string role { get; set; } = string.Empty;
        public bool IsLoggedIn { get; set; } = false;
        public string EmpId { get; set; } = string.Empty;
        public string UserOtp { get; set; } = string.Empty;
        public string RefranceId { get; set; } = string.Empty;
        
        public string DateOfBirth { get; set; } = string.Empty;
        public string gender { get; set; } = string.Empty;
        public string address { get; set; } = string.Empty;
        public string city { get; set; } = string.Empty;
        public string state { get; set; } = string.Empty;
        public string zip { get; set; } = string.Empty;
        public string contory { get; set; } = string.Empty;
        public string profileName { get; set; } = string.Empty;
    }
}
