using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DependentProject;

namespace MainProject
{
    internal class Program
    {
        static void Main(string[] args)
        {
            LibInterface lib = new LibInterface();
            lib.Execute();

            return;
        }
    }
}
