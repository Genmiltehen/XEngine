using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XEngine.Core.Input.InputAxis
{
    public class AxisSettings
    {
                                public string Positive { get; set; } = "";
                                public string Negative { get; set; } = "";

                                public float Sensitivity { get; set; } = 3f;

                                public float Gravity { get; set; } = 3f;
    }
}
