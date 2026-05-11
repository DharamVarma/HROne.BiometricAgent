using System.ComponentModel.DataAnnotations;

namespace HROne.BiometricAgentModel
{
    public class Pager
    {
        [Range(0, int.MaxValue)]
        public int PageNumber { get; set; }

        [Range(0, int.MaxValue)]
        public int PageSize { get; set; }
    }
}
