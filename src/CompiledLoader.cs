using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Oxide.Plugins
{
    /// <summary>
    /// Loader for compiled .NET DLLs.
    /// </summary>
    public class CompiledLoader : PluginLoader
    {
        public override string FileExtension => ".dll";

        /// <summary>
        /// external dependency.
        /// </summary>
        private static CSharpExtension extension;

        /// <summary>
        /// Inject <see cref="CSharpExtension"/> dependency.
        /// </summary>
        /// <param name="extension"></param>
        public CompiledLoader(CSharpExtension extension)
        {
            CompiledLoader.extension = extension;
        }

        /// <summary>
        /// Attempt to synchronously load compiled plugin.
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public override Plugin Load(string directory, string name)
        {
            // first read and patch and assembly in memory.
            var rawAssembly = PatchAssemblyName(File.ReadAllBytes($"{directory}/{name}.dll"));

            // we never need to use the loader argument so simply pass null.
            var plugin   = new CompilablePlugin(extension, null, directory, name);
            var assembly = new CompiledAssembly(name, new CompilablePlugin[] { plugin }, rawAssembly, 0.0f);

            plugin.CompiledAssembly = assembly;

            // finally load the plugin.
            plugin.LoadPlugin(p1 =>
            {
                if (p1 != null)
                    LoadedPlugins[p1.Name] = p1;
            });

            return null;
        }

        /// <summary>
        /// Attempt to synchronously eject old plugin and reload new one.
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="name"></param>
        public override void Reload(string directory, string name)
        {
            Interface.Oxide.UnloadPlugin(name);
            Load(directory, name);
        }

        /// <summary>
        /// Called when the plugin manager is unloading a plugin that was loaded by this plugin loader
        /// </summary>
        /// <param name="plugin"></param>
        public override void Unloading(Plugin plugin)
        {
            LoadedPlugins.Remove(plugin.Name);
        }

        /// <summary>
        /// Patch assembly name to allow reloading of the same image.
        /// </summary>
        /// <param name="rawAssembly"></param>
        /// <returns></returns>
        private byte[] PatchAssemblyName(byte[] rawAssembly)
        {
            var stream     = new MemoryStream(rawAssembly);
            var definition = Mono.Cecil.AssemblyDefinition.ReadAssembly(stream);

            // ensure this number is never the same by using guid hash code as seed.
            var random   = new System.Random(Guid.NewGuid().GetHashCode());
            var randomId = random.Next(int.MaxValue).ToString();

            // append random number to end of assembly name.
            definition.Name.Name       += randomId;
            definition.MainModule.Name += randomId;

            // write the modifed assembly and return.
            using(var ms = new MemoryStream())
            {
                definition.Write(ms, new Mono.Cecil.WriterParameters());
                return ms.ToArray();
            }
        }
    }
}
