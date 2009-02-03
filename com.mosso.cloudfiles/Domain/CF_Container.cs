using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Authentication;
using com.mosso.cloudfiles.domain.request;
using com.mosso.cloudfiles.domain.response;
using com.mosso.cloudfiles.exceptions;

namespace com.mosso.cloudfiles.domain
{
    public interface IContainer
    {
        string Name { get; }
        IObject AddObject(string objectName);
        IObject AddObject(string objectName, Dictionary<string, string> metaTags);
        IObject AddObject(Stream localObjectStream, string remoteObjectName);
        IObject AddObject(Stream localObjectStream, string remoteObjectName, Dictionary<string, string> metaTags);
        void DeleteObject(string objectName);
        void MarkAsPublic();
        bool ObjectExists(string objectName);
        Uri PublicUrl { get; set; }
        Uri CDNManagementUrl { get; set; }
        string AuthToken { get; set; }
        Uri StorageUrl { get; set; }
        string StorageToken { get; set; }
        UserCredentials UserCredentials { get; set; }
    }

    public class CF_Container : IContainer
    {
        private string containerName;
        protected List<IObject> objects;

        public CF_Container(string containerName)
        {
            objects = new List<IObject>();
            this.containerName = containerName;
        }

        public string Name
        {
            get { return containerName; }
        }

        public string AuthToken { get; set; }
        public Uri PublicUrl { get; set; }
        public Uri CDNManagementUrl { get; set; }
        public Uri StorageUrl { get; set; }
        public string StorageToken { get; set; }
        public UserCredentials UserCredentials { get; set; }

        public IObject AddObject(string objectName)
        {
            return AddObject(objectName, new Dictionary<string, string>());
        }

        public IObject AddObject(string objectName, Dictionary<string, string> metaTags)
        {
            if (string.IsNullOrEmpty(objectName))
                throw new ArgumentNullException();

            CloudFilesPutObject(objectName, metaTags);

            CF_Object @object = new CF_Object(objectName);
            @object.PublicUrl = PublicUrl;
            @object.MetaTags = metaTags;

            if (objects.Find(x => x.Name == objectName) == null)
                objects.Add(@object);

            return @object;
        }

        public IObject AddObject(Stream localObjectStream, string remoteObjectName)
        {
            return AddObject(localObjectStream, remoteObjectName, new Dictionary<string, string>());
        }

        public IObject AddObject(Stream localObjectStream, string remoteObjectName, Dictionary<string, string> metaTags)
        {
            if (string.IsNullOrEmpty(remoteObjectName)
                || localObjectStream == null)
                throw new ArgumentNullException();

            CloudFilesPutObject(localObjectStream, remoteObjectName, metaTags);

            CF_Object @object = new CF_Object(remoteObjectName);
            @object.PublicUrl = PublicUrl;
            @object.MetaTags = metaTags;

            if (objects.Find(x => x.Name == remoteObjectName) == null)
                objects.Add(@object);

            return @object;
        }

        public void DeleteObject(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                throw new ArgumentNullException();

            CloudFilesDeleteObject(objectName);
            if(objects.Find(x => x.Name == objectName) == null)
                throw new StorageItemNotFoundException();
            objects.Remove(objects.Find(x => x.Name == objectName));
        }


        public void MarkAsPublic()
        {
            CloudFilesMarkContainerPublic();
        }

        public bool ObjectExists(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                throw new ArgumentNullException();

            return CloudFilesHeadObject(objectName)
                   && objects.Contains(objects.Find(x => x.Name == objectName));
        }

        protected virtual void CloudFilesPutObject(string objectName, Dictionary<string, string> metaTags)
        {
            if (string.IsNullOrEmpty(containerName) ||
                string.IsNullOrEmpty(objectName))
                throw new ArgumentNullException();

            string remoteName = Path.GetFileName(objectName);
            string localName = objectName.Replace("/", "\\");
            try
            {
                PutStorageItem putStorageItem = new PutStorageItem(StorageUrl.ToString(), containerName, remoteName, localName, StorageToken, metaTags);
                PutStorageItemResponse putStorageItemResponse = new ResponseFactory<PutStorageItemResponse>().Create(new CloudFilesRequest(putStorageItem, UserCredentials.ProxyCredentials));
            }
            catch (WebException webException)
            {
                HttpWebResponse webResponse = (HttpWebResponse)webException.Response;
                if (webResponse.StatusCode == HttpStatusCode.BadRequest)
                    throw new ContainerNotFoundException("The requested container does not exist");
                if (webResponse.StatusDescription == "422")
                    throw new InvalidETagException("The ETag supplied in the request does not match the ETag calculated by the server");
            }
        }

        protected virtual void CloudFilesPutObject(Stream localObjectStream, string remoteObjectName, Dictionary<string, string> metaTags)
        {
            if (string.IsNullOrEmpty(containerName) ||
                string.IsNullOrEmpty(remoteObjectName))
                throw new ArgumentNullException();


            string remoteName = Path.GetFileName(remoteObjectName);
            try
            {
                PutStorageItem putStorageItem = new PutStorageItem(StorageUrl.ToString(), containerName, remoteName, localObjectStream, StorageToken, metaTags);
                PutStorageItemResponse putStorageItemResponse = new ResponseFactory<PutStorageItemResponse>().Create(new CloudFilesRequest(putStorageItem, UserCredentials.ProxyCredentials));
            }
            catch (WebException webException)
            {
                HttpWebResponse webResponse = (HttpWebResponse)webException.Response;
                if (webResponse.StatusCode == HttpStatusCode.BadRequest)
                    throw new ContainerNotFoundException("The requested container does not exist");
                if (webResponse.StatusDescription == "422")
                    throw new InvalidETagException("The ETag supplied in the request does not match the ETag calculated by the server");
            }
        }

        protected virtual void CloudFilesDeleteObject(string objectName)
        {
            if (string.IsNullOrEmpty(containerName) ||
                string.IsNullOrEmpty(objectName))
                throw new ArgumentNullException();

            DeleteStorageItem deleteStorageItem = new DeleteStorageItem(StorageUrl.ToString(), containerName, objectName, StorageToken);
            try
            {
                new ResponseFactory<DeleteStorageItemResponse>().Create(new CloudFilesRequest(deleteStorageItem));
            }
            catch (WebException we)
            {
                HttpWebResponse response = (HttpWebResponse)we.Response;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                    throw new StorageItemNotFoundException("The requested storage object for deletion does not exist");
            }
        }

        protected virtual bool CloudFilesHeadObject(string objectName)
        {
            if (string.IsNullOrEmpty(containerName) ||
               string.IsNullOrEmpty(objectName))
                throw new ArgumentNullException();

            GetStorageItemInformation getStorageItemInformation = new GetStorageItemInformation(StorageUrl.ToString(), containerName, objectName, StorageToken);
            try
            {
                GetStorageItemInformationResponse getStorageItemResponse = new ResponseFactory<GetStorageItemInformationResponse>().Create(new CloudFilesRequest(getStorageItemInformation, UserCredentials.ProxyCredentials));
            }
            catch (WebException we)
            {
                HttpWebResponse response = (HttpWebResponse)we.Response;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                    return false;
            }
            return true;
        }

        protected virtual void CloudFilesMarkContainerPublic()
        {
            SetContainerAsPublicRequest request = new SetContainerAsPublicRequest(CDNManagementUrl.ToString(), AuthToken, Name);
            MarkContainerAsPublic(request);
        }

        private void MarkContainerAsPublic(SetContainerAsPublicRequest request)
        {
            SetContainerAsPublicResponse response = null;
            try
            {
                response = new ResponseFactory<SetContainerAsPublicResponse>().Create(new CloudFilesRequest(request));
            }
            catch (WebException we)
            {
                HttpStatusCode code = ((HttpWebResponse) we.Response).StatusCode;
                if (code == HttpStatusCode.Unauthorized)
                {
                    throw new InvalidCredentialException("You do not have permission to mark this container as public.");
                }
                if (code == HttpStatusCode.Accepted)
                {
                    throw new ContainerAlreadyPublicException("The specified container is already marked as public.");
                }
            }

            PublicUrl = (response == null ? null : new Uri(response.Headers[Constants.X_CDN_URI]));
        }
    }
}