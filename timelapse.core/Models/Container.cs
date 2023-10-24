using System.ComponentModel.DataAnnotations;

namespace timelapse.core.models;

public enum ContainerProvider{
    Azure_Blob,
    AWS_S3
}

public abstract class Container
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int OwnerOrganisationId { get; set; }
    public Organisation? OwnerOrganisation { get; set; }
    // public List<DeviceProjectContract> DeviceProjectContracts { get; } = new List<DeviceProjectContract>();

    public abstract Uri Upload(string blobName, Stream stream);
}

public class Container_AWS_S3: Container
{
    public string Region { get; set; }
    public string BucketName { get; set; }

    public string AccessKey {get; set; }
    public string SecretKey { get; set; }

    public override Uri Upload(string blobName, Stream stream){
        throw new NotImplementedException();
    }
}

public class Container_Azure_Blob: Container
{
    // public string StorageAccountName { get; set; }
    public string ConnectionString { get; set; }

    public override Uri Upload(string blobName, Stream stream){
        throw new NotImplementedException();
    }
}