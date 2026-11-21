using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace App.Editor
{
    /// Minimal static file server for a local WebGL build (browsers block WebGL builds opened via file://).
    /// Lives until the next domain reload or Editor quit.
    internal static class WebGLServer
    {
        private static HttpListener _listener;
        private static string _root;
        private static string _url;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
        }

        /// Serves the folder (reusing the running server for the same folder) and returns its URL.
        public static string Serve(string root)
        {
            root = Path.GetFullPath(root);

            if (_listener != null && _listener.IsListening && _root == root)
                return _url;

            Stop();

            _root = root;
            _url = $"http://localhost:{GetFreePort()}/";
            _listener = new HttpListener();
            _listener.Prefixes.Add(_url);
            _listener.Start();

            var listener = _listener;
            new Thread(() => Listen(listener, root)) { IsBackground = true, Name = "WebGL build server" }.Start();

            Debug.Log($"[WebGLServer] Serving {root} at {_url}");
            return _url;
        }

        private static void Stop()
        {
            if (_listener == null)
                return;

            try { _listener.Close(); } catch { /* already closed */ }
            _listener = null;
        }

        private static void Listen(HttpListener listener, string root)
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = listener.GetContext();
                }
                catch (Exception)
                {
                    return; // listener stopped
                }

                ThreadPool.QueueUserWorkItem(_ => Respond(context, root));
            }
        }

        private static void Respond(HttpListenerContext context, string root)
        {
            var response = context.Response;

            try
            {
                string relative = Uri.UnescapeDataString(context.Request.Url.AbsolutePath).TrimStart('/');
                string path = Path.GetFullPath(Path.Combine(root, relative.Length == 0 ? "index.html" : relative));

                if (Directory.Exists(path))
                    path = Path.Combine(path, "index.html");

                // Reject paths escaping the build folder
                if (!path.StartsWith(root + Path.DirectorySeparatorChar) || !File.Exists(path))
                {
                    response.StatusCode = 404;
                    return;
                }

                // Pre-compressed files without decompression fallback (*.js.gz, *.wasm.br): let the browser decompress them
                string extension = Path.GetExtension(path);
                string contentPath = path;

                if (extension == ".gz" || extension == ".br" || extension == ".unityweb")
                    contentPath = path.Substring(0, path.Length - extension.Length);

                if (extension == ".gz" || extension == ".br")
                    response.AddHeader("Content-Encoding", extension == ".gz" ? "gzip" : "br");

                byte[] bytes = File.ReadAllBytes(path);

                // Gzip-compressed *.unityweb (decompression fallback): native browser decompression starts faster
                if (extension == ".unityweb" && bytes.Length > 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
                    response.AddHeader("Content-Encoding", "gzip");

                response.ContentType = GetContentType(Path.GetExtension(contentPath));
                response.AddHeader("Cache-Control", "no-cache");
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception e)
            {
                response.StatusCode = 500;
                Debug.LogWarning($"[WebGLServer] {context.Request.Url}: {e.Message}");
            }
            finally
            {
                try { response.Close(); } catch { /* client went away */ }
            }
        }

        private static string GetContentType(string extension)
        {
            switch (extension)
            {
                case ".html": return "text/html; charset=utf-8";
                case ".js": return "application/javascript";
                case ".wasm": return "application/wasm";
                case ".css": return "text/css";
                case ".json": return "application/json";
                case ".png": return "image/png";
                case ".jpg": return "image/jpeg";
                case ".ico": return "image/x-icon";
                case ".svg": return "image/svg+xml";
                default: return "application/octet-stream"; // .data, .unityweb, ...
            }
        }

        private static int GetFreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }
}
