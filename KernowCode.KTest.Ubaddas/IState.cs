using System;

namespace KernowCode.KTest.Ubaddas
{
    /// <summary>
    /// Internal state helper for UBADDAS
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// Stores the IPersona type specified previously in the BDD statement via the 'As' statement
        /// </summary>
        Type CurrentPersonaType { get; set; }
    }
}