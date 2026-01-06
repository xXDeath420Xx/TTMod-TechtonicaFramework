namespace TechtonicaFramework.Core
{
    /// <summary>
    /// Base interface for all framework modules.
    /// Provides standard lifecycle methods.
    /// </summary>
    public interface IFrameworkModule
    {
        /// <summary>
        /// Called when the module is initialized (game loading).
        /// </summary>
        void Initialize();

        /// <summary>
        /// Called every frame for modules that need updates.
        /// </summary>
        void Update();

        /// <summary>
        /// Called when the module is shut down (game unloading).
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Returns whether the module is currently active.
        /// </summary>
        bool IsActive { get; }
    }
}
