namespace FreeMote.Plugins
{
    public interface IPsbPlugin
    {
    }
    public interface IPsbPluginInfo
    {
        /// <summary>
        /// Plugin Name
        /// </summary>
        string Name { get; }
        /// <summary>
        /// Plugin Author
        /// </summary>
        string Author { get; }
        /// <summary>
        /// Comment
        /// </summary>
        string Comment { get; }
    }
}
