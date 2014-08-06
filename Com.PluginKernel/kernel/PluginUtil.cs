﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Ops.Framework.Graphic;
using System.Drawing;
using Ops.Framework.Net;
using Ops.Utility;
using System.Reflection;

namespace Com.PluginKernel
{
    /// <summary>
    /// 插件工具
    /// </summary>
    public static class PluginUtil
    {

        /// <summary>
        /// 插件属性字典
        /// </summary>
        private static IDictionary<int, PluginPackAttribute> attrDicts;

        /// <summary>
        /// 迭代插件
        /// </summary>
        /// <param name="handler"></param>
        public static void Iterate(PluginHandler handler)
        {
            foreach (IPlugin p in PluginAppBase.plugins.Keys)
            {
                handler(p, PluginAppBase.plugins[p]);
            }
        }

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <returns></returns>
        public static PluginPackAttribute GetAttribute<T>()
            where T : IPlugin
        {
            //string key = typeof(T).FullName;
            //if (attrDicts == null)
            //{
            //    attrDicts = new Dictionary<string, PluginPackAttribute>();
            //}

            //if (!attrDicts.Keys.Contains(key))
            //{
            //   PluginPackAttribute attr = (PluginPackAttribute)typeof(T).Assembly
            //       .GetCustomAttributes(typeof(PluginPackAttribute), false)[0];
            //    attrDicts.Add(key,attr);
            //    return attr;
            //}

            //return attrDicts[key];

            return GetAttributeByType(typeof(T));
        }

        public static PluginPackAttribute GetAttribute(IPlugin plugin)
        {
            return GetAttributeByType(plugin.GetType());
        }

        private static PluginPackAttribute GetAttributeByType(Type type)
        {
            // if (t.IsSubclassOf(typeof(IPlugin))) { }
            int key = type.Assembly.GetHashCode();
            if (attrDicts == null)
            {
                attrDicts = new Dictionary<int, PluginPackAttribute>();
            }

            if (!attrDicts.Keys.Contains(key))
            {
                object[] attrs = type.Assembly.GetCustomAttributes(typeof(PluginPackAttribute), false);
                if (attrs.Length == 0) throw new Exception();

                PluginPackAttribute attr = (PluginPackAttribute)attrs[0];
                attrDicts.Add(key, attr);
                return attr;
            }

            return attrDicts[key];
        }

        /// <summary>
        /// 获取插件
        /// </summary>
        /// <param name="workerIndent"></param>
        /// <returns></returns>
        public static IPlugin GetPlugin(string workerIndent, out PluginPackAttribute attr)
        {
            var plugins = PluginAppBase.plugins;

            foreach (IPlugin p in plugins.Keys)
            {
                if (String.Compare(workerIndent, plugins[p].WorkIndent, true) == 0)
                {
                    attr = plugins[p];
                    return p;
                }
            }

            attr = null;
            return null;
        }

        /// <summary>
        /// 获取插件包信息
        /// </summary>
        /// <param name="workerIndent"></param>
        /// <returns></returns>
        public static PluginPackAttribute GetPluginPackAttribute(string workerIndent)
        {
            var plugins = PluginAppBase.plugins;

            foreach (IPlugin p in plugins.Keys)
            {
                if (String.Compare(workerIndent, plugins[p].WorkIndent, true) == 0)
                {
                    return plugins[p];
                }
            }
            return null;
        }
        /// <summary>
        /// 获取插件
        /// </summary>
        /// <param name="workerIndent"></param>
        /// <returns></returns>
        public static IEnumerable<IPlugin> GetPlugins(string workerIndent)
        {
            var plugins = PluginAppBase.plugins;

            foreach (IPlugin p in plugins.Keys)
            {
                if (String.Compare(workerIndent, plugins[p].WorkIndent, true) == 0)
                {
                    yield return p;
                }
            }
        }


        /// <summary>
        /// 获取插件的图标
        /// </summary>
        /// <param name="workerIndent"></param>
        /// <returns></returns>
        public static byte[] GetPluginIcon(string workerIndent, int width, int height, string defaultIconPath)
        {
            string icon = null;
            var iconExist = false;
            byte[] data;

            Iterate((p, a) =>
            {
                if (String.Compare(a.WorkIndent, workerIndent) == 0)
                {
                    icon = String.Format("{0}{1}{2}/{3}", 
                        AppDomain.CurrentDomain.BaseDirectory,
                        PluginConfig.PLUGIN_DIRECTORY,
                        workerIndent, a.Icon);
                }
            });

            if (!String.IsNullOrEmpty(icon))
            {
                iconExist = File.Exists(icon);
            }

        resetIcon:
            if (!iconExist)
            {
                icon = defaultIconPath;
            }

            var bit = (Bitmap)null;
            try
            {
                using (bit = new Bitmap(icon))
                {
                    var ms = GraphicsHelper.MakeThumbnail(bit, ImageSizeMode.AutoSuit, width, height, null);
                    data = ms.ToArray();
                    ms.Dispose();
                    bit.Dispose();
                }
            }
            catch
            {
                iconExist = false;
                goto resetIcon;
            }
            finally
            {
                if (bit != null)
                {
                    bit.Dispose();
                }
            }

            return data;
        }

        /// <summary>
        /// 移除插件
        /// </summary>
        /// <param name="workerIndent"></param>
        /// <returns></returns>
        public static bool RemovePlugin(string workerIndent)
        {
            PluginPackAttribute attr;
            var plugin = GetPlugin(workerIndent, out attr);

            if (plugin != null)
            {
                try
                {
                    plugin.Uninstall();
                }
                catch
                {
                }

                var dirName = String.Format("{0}{1}/{2}/",
                                               AppDomain.CurrentDomain.BaseDirectory,
                                               PluginConfig.PLUGIN_DIRECTORY,
                                               attr.WorkIndent
                                              );

                if (Directory.Exists(dirName))
                {
                    Directory.Delete(dirName, true);

                    var dllName = String.Format("{0}{1}/{2}",
                                                   AppDomain.CurrentDomain.BaseDirectory,
                                                   PluginConfig.PLUGIN_DIRECTORY,
                                                   plugin.GetType().Assembly.Location
                                                  );

                    File.Delete(dllName);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 安装/升级插件
        /// </summary>
        /// <param name="url"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        public static bool InstallPlugin(string url, PluginHandler<PluginPackAttribute> handler)
        {
            var installResult = false;
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var pluginPath = String.Concat(
                appDirectory,
                PluginConfig.PLUGIN_DIRECTORY);

            var tempDir = String.Concat(appDirectory,PluginConfig.PLUGIN_TMP_DIRECTORY,"plugin/");
            var fileName = tempDir + "dl_pack_"+String.Empty.RandomLetters(16)+".zip";

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
            Directory.CreateDirectory(tempDir).Create();

            var dir = new DirectoryInfo(tempDir);

            var data = WebClient.DownloadFile(url, null);
            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                fs.Write(data, 0, data.Length);
                fs.Flush();
                fs.Dispose();
            }

            IList<PluginPackAttribute> pluginAttrs = new List<PluginPackAttribute>(GetPluginPackInfo(fileName));

            if (pluginAttrs.Count != 0)
            {
                ZipUtility.UncompressFile(pluginPath, fileName, false);
                foreach (FileInfo file in dir.GetFiles("*.dll"))
                {
                    File.Delete(pluginPath + file.Name);
                    file.MoveTo(pluginPath + file.Name);
                }

                if (handler != null)
                {
                    var result = false;
                    foreach (PluginPackAttribute attr in pluginAttrs)
                    {
                        handler(attr, ref result);
                    }
                }

                installResult = true;
            }

            Directory.Delete(tempDir, true);

            return installResult;
        }

        /// <summary>
        /// 获取插件包的信息
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static IEnumerable<PluginPackAttribute> GetPluginPackInfo(string fileName)
        {
            var tempDir = String.Concat(AppDomain.CurrentDomain, PluginConfig.PLUGIN_TMP_DIRECTORY, "tmp/");

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
            Directory.CreateDirectory(tempDir).Create();

            var dir = new DirectoryInfo(tempDir);

            ZipUtility.UncompressFile(tempDir, fileName, false);

            Assembly ass;
            var files = dir.GetFiles("*.dll");
            foreach (FileInfo f in files)
            {
                ass = Assembly.Load(File.ReadAllBytes(f.FullName));

                var attbs = ass.GetCustomAttributes(typeof(PluginPackAttribute), false);
                foreach (object attb in attbs)
                {
                    if (attb is PluginPackAttribute)
                    {
                        yield return (PluginPackAttribute)attb;
                    }
                }
            }
        }
    }
}
