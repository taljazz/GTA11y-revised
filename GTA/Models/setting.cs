#region Imports
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#endregion

namespace GrandTheftAccessibility
{
    class Setting
    {
        #region Fields
        public string displayName;
        public int value;
        public string id;
        #endregion
        #region Constructors
        public Setting(string id, string displayName, int value)
        {
            this.id = id;
            this.displayName = displayName;
            this.value = value;
        }
        #endregion
    }
}