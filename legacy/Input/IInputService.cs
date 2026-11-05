using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XEngine.Core.Input
{
    public interface IInputService
    {
                                        public bool IsActionInactive(string actionName);

                                        public bool IsActionActive(string actionName);

                                        public bool IsActionJustPressed(string actionName);

                                        public bool IsActionJustReleased(string actionName);

                                        public float GetAxis(string axisName);
    }
}
