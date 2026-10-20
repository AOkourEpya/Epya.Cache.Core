using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Epya.Cache.Common
{
    public class EntityMetadataDto
    {
        public string EntityName { get; set; }
        public List<ForeignKeyMetadataDto> ForeignKeys { get; set; } = new List<ForeignKeyMetadataDto>();
    }

    public class ForeignKeyMetadataDto
    {
        public string ForeignKeyProperty { get; set; }
        public string RelatedEntity { get; set; }
        public string RelatedEntityTypeFullName { get; set; }
        public string NavigationProperty { get; set; }
        public string NavigationPropertyTypeFullName { get; set; }
        public string InverseCollectionNavigation { get; set; }  // e.g., "Orders"

        public bool IsRequired { get; set; }
        public string RelationType { get; set; }  // "Single" or "Collection"
    }
}
