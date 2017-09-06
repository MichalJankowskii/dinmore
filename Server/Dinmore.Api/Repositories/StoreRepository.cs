﻿using dinmore.api.Interfaces;
using dinmore.api.Models;
using Dinmore.Api.Models;
using Dinmore.Domain;
using Microsoft.Extensions.Options;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace dinmore.api.Repositories
{

    public class StoreRepository : IStoreRepository
    {
        private readonly AppSettings _appSettings;

        public StoreRepository(IOptions<AppSettings> appSettings)
        {
            _appSettings = appSettings.Value;
        }

        public async Task StoreDevice(Device device)
        {
            //get cloudtable
            var table = await GetCloudTable(_appSettings.TableStorageConnectionString, _appSettings.StoreDeviceContainerName);

            DeviceStorageTableEntity deviceStorageTableEntity = new DeviceStorageTableEntity(device.Id.ToString(), _appSettings.StoreDevicePartitionKey);
            deviceStorageTableEntity.DeviceLabel = device.DeviceLabel;
            deviceStorageTableEntity.Exhibit = device.Exhibit;
            deviceStorageTableEntity.Venue = device.Venue;
            deviceStorageTableEntity.Interactive = device.Interactive;
            deviceStorageTableEntity.VerbaliseSystemInformationOnBoot = device.VerbaliseSystemInformationOnBoot;
            deviceStorageTableEntity.SoundOn = device.SoundOn;
            deviceStorageTableEntity.ResetOnBoot = device.ResetOnBoot;
            deviceStorageTableEntity.VoicePackageUrl = device.VoicePackageUrl;
            deviceStorageTableEntity.QnAKnowledgeBaseId = device.QnAKnowledgeBaseId;

            TableOperation insertOperation = TableOperation.Insert(deviceStorageTableEntity);

            await table.ExecuteAsync(insertOperation);
        }

        public async Task DeleteDevice(string deviceId)
        {
            //get cloudtable
            var table = await GetCloudTable(_appSettings.TableStorageConnectionString, _appSettings.StoreDeviceContainerName);

            // Create a retrieve operation that expects a customer entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<DeviceStorageTableEntity>(_appSettings.StoreDevicePartitionKey, deviceId);

            // Execute the operation.
            TableResult retrievedResult = await table.ExecuteAsync(retrieveOperation);

            // Assign the result to a CustomerEntity.
            var deleteEntity = (DeviceStorageTableEntity)retrievedResult.Result;


            // Create the Delete TableOperation.
            if (deleteEntity != null)
            {
                // Create the Delete TableOperation.
                TableOperation deleteOperation = TableOperation.Delete(deleteEntity);

                // Execute the operation.
                await table.ExecuteAsync(deleteOperation);
            }

        }

        public async Task<Device> GetDevice(string deviceId)
        {
            //get cloudtable
            var table = await GetCloudTable(_appSettings.TableStorageConnectionString, _appSettings.StoreDeviceContainerName);

            // Create a retrieve operation that takes a customer entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<DeviceStorageTableEntity>(_appSettings.StoreDevicePartitionKey, deviceId);

            // Execute the retrieve operation.
            TableResult retrievedResult = await table.ExecuteAsync(retrieveOperation);

            if (retrievedResult.Result != null)
            {
                // get the result and create a new device from the data
                var deviceResult = (DeviceStorageTableEntity)retrievedResult.Result;

                // Create a new Device object from result
                var device = CastDeviceStorageTableEntityToDevice(deviceResult);

                return device;
            }
            else
            {
                return null;
            }
        }

        public async Task<IEnumerable<Device>> GetDevices()
        {
            //get cloudtable
            var table = await GetCloudTable(_appSettings.TableStorageConnectionString, _appSettings.StoreDeviceContainerName);

            TableContinuationToken token = null;

            var entities = new List<DeviceStorageTableEntity>();
            do
            {
                var queryResult = await table.ExecuteQuerySegmentedAsync(new TableQuery<DeviceStorageTableEntity>(), token);
                entities.AddRange(queryResult.Results);
                token = queryResult.ContinuationToken;
            } while (token != null);

            // create list of device objects from the storage entities
            var devices = new List<Device>();
            foreach (var deviceStorageTableEntity in entities)
            {
                // Create a new Device object from result
                var device = CastDeviceStorageTableEntityToDevice(deviceStorageTableEntity);

                devices.Add(device);
            }

            return devices;
        }

        public async Task<Device> ReplaceDevice(Device device)
        {
            //upload voice package if there is one
            var fileName = string.Empty;
            if (device.VoicePackage.Length > 0)
            {
                // Retrieve storage account from connection string.
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_appSettings.TableStorageConnectionString);

                // Create the blob client.
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                // Retrieve reference to a previously created container.
                CloudBlobContainer container = blobClient.GetContainerReference(_appSettings.StoreVoicePackageContainerName);

                // Retrieve reference to a blob named "myblob".
                fileName = $"{device.Exhibit}-{device.DeviceLabel}-{DateTime.Now.ToString("yyyyMMddHHmmss")}.zip";
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);
                blockBlob.Properties.ContentType = "application/zip";

                // Create or overwrite the blob with contents from a local file.
                await blockBlob.UploadFromByteArrayAsync(device.VoicePackage, 0, device.VoicePackage.Length);
            }

            // Construct the new voce package url if we have a file name, otherwise default to what ws passed in the Device object
            var voicePackageUrl = device.VoicePackageUrl;
            if (!string.IsNullOrEmpty(fileName))
            {
                voicePackageUrl = $"{_appSettings.StoreVoicePackagesRootUrl}{fileName}";
            }

            //get cloudtable
            var table = await GetCloudTable(_appSettings.TableStorageConnectionString,_appSettings.StoreDeviceContainerName);

            // Create a retrieve operation that takes a customer entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<DeviceStorageTableEntity>(_appSettings.StoreDevicePartitionKey, device.Id.ToString());

            // Execute the operation.
            TableResult retrievedResult = await table.ExecuteAsync(retrieveOperation);

            // Assign the result to a CustomerEntity object.
            var updateEntity = (DeviceStorageTableEntity)retrievedResult.Result;

            if (updateEntity != null)
            {
                // Change the entity
                updateEntity.Exhibit = device.Exhibit;
                updateEntity.DeviceLabel = device.DeviceLabel;
                updateEntity.Venue = device.Venue;
                updateEntity.Interactive = device.Interactive;
                updateEntity.VerbaliseSystemInformationOnBoot = device.VerbaliseSystemInformationOnBoot;
                updateEntity.SoundOn = device.SoundOn;
                updateEntity.ResetOnBoot = device.ResetOnBoot;
                updateEntity.VoicePackageUrl = voicePackageUrl;
                updateEntity.QnAKnowledgeBaseId = device.QnAKnowledgeBaseId;

                // Create the Replace TableOperation.
                TableOperation updateOperation = TableOperation.Replace(updateEntity);

                // Execute the operation.
                await table.ExecuteAsync(updateOperation);

                // Create a new Device object from result
                var newDevice = CastDeviceStorageTableEntityToDevice(updateEntity);

                return newDevice;
            }
            else
            {
                return null;
            }
        }

        public async Task StorePatrons(List<Patron> patrons)
        {
            //get cloudtable
            var table = await GetCloudTable(_appSettings.TableStorageConnectionString, _appSettings.StorePatronContainerName);

            //insert an entity (row) per patron
            foreach (var patron in patrons)
            {
                var persistedFaceId = patron.PersistedFaceId;
                var sightingId = Guid.NewGuid().ToString(); //This is a unique ID for the sighting

                PatronStorageTableEntity patronStorageTableEntity = new PatronStorageTableEntity(persistedFaceId, sightingId);
                patronStorageTableEntity.Device = patron.DeviceLabel;
                patronStorageTableEntity.Exhibit = patron.Exhibit;
                patronStorageTableEntity.Venue = patron.Venue;
                patronStorageTableEntity.Gender = patron.FaceAttributes.gender;
                patronStorageTableEntity.Age = Math.Round(patron.FaceAttributes.age, 0);
                patronStorageTableEntity.PrimaryEmotion = patron.PrimaryEmotion;
                patronStorageTableEntity.TimeOfSighting = (DateTime)patron.Time;
                patronStorageTableEntity.Smile = patron.FaceAttributes.smile;
                patronStorageTableEntity.Glasses = patron.FaceAttributes.glasses;
                patronStorageTableEntity.FaceMatchConfidence = (double)patron.FaceMatchConfidence;

                TableOperation insertOperation = TableOperation.Insert(patronStorageTableEntity);

                await table.ExecuteAsync(insertOperation);
            }
        }

        private Device CastDeviceStorageTableEntityToDevice(DeviceStorageTableEntity deviceEntity)
        {
            var device = new Device()
            {
                Id = new Guid(deviceEntity.RowKey),
                Exhibit = deviceEntity.Exhibit,
                DeviceLabel = deviceEntity.DeviceLabel,
                Venue = deviceEntity.Venue,
                Interactive = deviceEntity.Interactive,
                ResetOnBoot = deviceEntity.ResetOnBoot,
                SoundOn = deviceEntity.SoundOn,
                VerbaliseSystemInformationOnBoot = deviceEntity.VerbaliseSystemInformationOnBoot,
                VoicePackageUrl = deviceEntity.VoicePackageUrl,
                QnAKnowledgeBaseId = deviceEntity.QnAKnowledgeBaseId
            };

            return device;
        }

        private async Task<CloudTable> GetCloudTable(string tableConnectionString, string containerName)
        {
            var storageAccount = CloudStorageAccount.Parse(tableConnectionString);

            var blobClient = storageAccount.CreateCloudTableClient();

            var table = blobClient.GetTableReference(containerName);

            await table.CreateIfNotExistsAsync();

            return table;
        }

    }
}
