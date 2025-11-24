using System.Xml.Linq;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace MyDigitalLibrary.Core.Services;

public class DataProtectionBlobXmlRepository : IXmlRepository
{
    private readonly BlobContainerClient _container;
    private readonly string _blobName;

    public DataProtectionBlobXmlRepository(BlobContainerClient container, string blobName)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _blobName = string.IsNullOrEmpty(blobName) ? "keys.xml" : blobName;
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        var blob = _container.GetBlobClient(_blobName);
        if (!blob.Exists()) return Array.Empty<XElement>();

        using var ms = new MemoryStream();
        blob.DownloadTo(ms);
        ms.Position = 0;
        var doc = XDocument.Load(ms);
        var elements = doc.Root?.Elements().ToList();
        return elements != null ? (IReadOnlyCollection<XElement>)elements : Array.Empty<XElement>();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        var blob = _container.GetBlobClient(_blobName);

        XDocument doc;
        if (blob.Exists())
        {
            using var ms = new MemoryStream();
            blob.DownloadTo(ms);
            ms.Position = 0;
            doc = XDocument.Load(ms);
            doc.Root?.Add(element);
        }
        else
        {
            doc = new XDocument(new XElement("root", element));
        }

        using var outMs = new MemoryStream();
        doc.Save(outMs);
        outMs.Position = 0;
        blob.Upload(outMs, overwrite: true);
    }
}
