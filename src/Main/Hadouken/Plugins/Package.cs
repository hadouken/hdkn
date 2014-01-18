﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using Hadouken.Framework.IO;
using Hadouken.Plugins.Metadata;
using Ionic.Zip;

namespace Hadouken.Plugins
{
    public sealed class Package : IPackage
    {
        private readonly IManifest _manifest;
        private readonly IFile[] _files;

        private Package(IManifest manifest, IFile[] files)
        {
            _manifest = manifest;
            _files = files;
        }

        public IManifest Manifest
        {
            get { return _manifest; }
        }

        public IFile[] Files
        {
            get { return _files; }
        }

        public byte[] Data { get; set; }

        public static bool TryParse(byte[] packageData, out IPackage package)
        {
            package = null;

            using (var ms = new MemoryStream(packageData))
            {
                Package p;

                if (!TryParse(ms, out p)) return false;

                p.Data = packageData;
                package = p;

                return true;
            }
        }

        private static bool TryParse(Stream stream, out Package package)
        {
            package = null;

            try
            {
                using (var zip = ZipFile.Read(stream))
                {
                    // Read manifest
                    var manifestEntry = zip.Entries.SingleOrDefault(e => e.FileName == Metadata.Manifest.FileName);

                    if (manifestEntry == null)
                        return false;

                    using (var memoryStream = new MemoryStream())
                    {
                        manifestEntry.Extract(memoryStream);
                        memoryStream.Position = 0;

                        IManifest manifest;

                        if (!Metadata.Manifest.TryParse(memoryStream, out manifest))
                            return false;

                        // Valid manifest, lets load all files
                        var files = new List<IFile>();

                        foreach (var entry in zip.Entries)
                        {
                            using (var ms = new MemoryStream())
                            {
                                entry.Extract(ms);

                                var data = ms.ToArray();
                                var streamFactory = new Func<MemoryStream>(() => new MemoryStream(data));

                                files.Add(new InMemoryFile(streamFactory)
                                {
                                    Name = entry.FileName
                                });
                            }
                        }

                        package = new Package(manifest, files.ToArray());
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
