﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using Palaso.Progress;

namespace WeShare.Transport
{
    public class SendQueue : IEnumerable<FileInfo>
    {
        public IEnumerator<FileInfo> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    class WeShareException : Exception
    {
        public WeShareException(string message) : base(message) {}
    }

    class WeShareOperationFailed : WeShareException
    {
        public WeShareOperationFailed(string message) : base(message) {}
    }
 
    public class WeShareTransport : IWeShareTransport
    {
        private readonly IProgress _progress;
        private readonly HgRepository _repo;
        private readonly string _targetLabel;
        private readonly IApiServer _apiServer;
        private List<WeShareFile> _filesOnServer;

        private const int InitialChunkSize = 5000;
        private const int MaximumChunkSize = 250000;
        private const int TimeoutInSeconds = 15;
        private const int TargetTimeInSeconds = TimeoutInSeconds / 3;
    	internal const string RevisionCacheFilename = "revisioncache.db";
		
    	///<summary>
        ///</summary>
        public WeShareTransport(HgRepository repo, string targetLabel, IApiServer apiServer, IProgress progress, SendQueue queue)
        {
            _repo = repo;
            _targetLabel = targetLabel;
            _apiServer = apiServer;
            _progress = progress;
            _filesOnServer = new List<WeShareFile>();
        }

        private string RepoIdentifier
        {
            get
            {
                if (_repo.Identifier != null)
                {
                    return _repo.Identifier;
                }
                return _apiServer.Host.Replace('.', '_') + '-' + _apiServer.ProjectId;
            }
        }

        public void Push()
        {
            GetFileHashesFromServer();

            foreach (WeShareFile file in GetFileListNeedingPush())
            {
                try
                {
                    PushOneFile(file);
                }
                catch (Exception)
                {
                    // oops
                    throw;
                    
                }
                
            }

            // loop over the send queue and push each file one by one to the server
        }

        private IEnumerable<WeShareFile> GetFileListNeedingPush()
        {
            var list = new List<WeShareFile>();

            
            
            return list;
        }

        private void GetFileHashesFromServer()
        {
            // TODO do API call to server and get back a JSON list of WeShareFiles.  Convert to WeShareFile list.

            // TODO update _filesOnServer
            _filesOnServer = new List<WeShareFile>();
        }

        public void PushOneFile(WeShareFile file)
        {
            var pushManager = new PushStorageManager(PathToLocalStorage, file.MD5);

            var req = new HgResumeApiParameters
                      {
                          RepoId = _apiServer.ProjectId,
                          TransId = pushManager.TransactionId,
                          StartOfWindow = 0,
                          FileSize = (int) file.FileInfo.Length
                      };
            req.ChunkSize = (req.FileSize < InitialChunkSize) ? req.FileSize : InitialChunkSize;
            _progress.ProgressIndicator.Initialize();

            _progress.WriteStatus("Sending data");
            int loopCtr = 0;
            do // loop until finished... or until the user cancels
            {
                loopCtr++;
                if (_progress.CancelRequested)
                {
                    throw new Palaso.CommandLineProcessing.UserCancelledException();
                }

                int dataRemaining = req.FileSize - req.StartOfWindow;
                if (dataRemaining < req.ChunkSize)
                {
                    req.ChunkSize = dataRemaining;
                }
                byte[] bundleChunk = pushManager.GetChunk(req.StartOfWindow, req.ChunkSize);

                /* API parameters
                 * $repoId, $baseHash, $bundleSize, $offset, $data, $transId
                 * */
                var response = PushOneChunk(req, bundleChunk);
                if (response.Status == PushStatus.NotAvailable)
                {
                    _progress.ProgressIndicator.Initialize();
                    _progress.ProgressIndicator.Finish();
                    return;
                }
                if (response.Status == PushStatus.Timeout)
                {
                    _progress.WriteWarning("Push operation timed out.  Retrying...");
                    continue;
                }
                if (response.Status == PushStatus.Fail)
                {
                    // This 'Fail' intentionally aborts the push attempt.  I think we can continue to go around the loop and retry. See Pull also. CP 2012-06
                    continue;
                    //var errorMessage = "Push operation failed";
                    //_progress.WriteError(errorMessage);
                    //throw new WeShareOperationFailed(errorMessage);
                }
                if (response.Status == PushStatus.Reset)
                {
                    FinishPush(req.TransId);
                    pushManager.Cleanup();
                    const string errorMessage = "Push failed: Server reset.";
                    _progress.WriteError(errorMessage);
                    throw new WeShareOperationFailed(errorMessage);
                }

                if (response.Status == PushStatus.Complete || req.StartOfWindow >= req.FileSize)
                {
                    if (response.Status == PushStatus.Complete)
                    {
                        _progress.WriteMessage("Finished sending");
                    } else
                    {
                        _progress.WriteMessage("Finished sending. Server unpacking data");
                    }
                    _progress.ProgressIndicator.Finish();

                    // TODO update our local knowledge of what the server has
                    pushManager.Cleanup();
                    return;
                }

                req.ChunkSize = response.ChunkSize;
                req.StartOfWindow = response.StartOfWindow;
                if (loopCtr == 1 && req.StartOfWindow > req.ChunkSize)
                {
                    string message = String.Format("Resuming push operation at {0} sent", GetHumanReadableByteSize(req.StartOfWindow));
                    _progress.WriteVerbose(message);
                }
                string eta = CalculateEstimatedTimeRemaining(req.FileSize, req.ChunkSize, req.StartOfWindow);
                _progress.WriteStatus(string.Format("Sending {0} {1}", GetHumanReadableByteSize(req.FileSize), eta));
                _progress.ProgressIndicator.PercentCompleted = (int)((long)req.StartOfWindow * 100 / req.FileSize);
            } while (req.StartOfWindow < req.FileSize);
        }

        private static string CalculateEstimatedTimeRemaining(int bundleSize, int chunkSize, int startOfWindow)
        {
            if (startOfWindow < 80000)
            {
                return ""; // wait until we've transferred 80K before calculating an ETA
            }
            int secondsRemaining = TargetTimeInSeconds*(bundleSize - startOfWindow)/chunkSize;
            if (secondsRemaining < 60)
            {
                //secondsRemaining = (secondsRemaining/5+1)*5;
                return "(less than 1 minute)";
            }
            int minutesRemaining = secondsRemaining/60;
            string minutesString = (minutesRemaining > 1) ? "minutes" : "minute";
            return String.Format("(about {0} {1})", minutesRemaining, minutesString);
        }

        private static string GetHumanReadableByteSize(int length)
        {
            // lifted from http://stackoverflow.com/questions/281640/how-do-i-get-a-human-readable-file-size-using-net
            try
            {
                string[] sizes = { "B", "KB", "MB", "GB" };
                int order = 0;
                while (length >= 1024 && order + 1 < sizes.Length)
                {
                    order++;
                    length = length / 1024;
                }
                return String.Format("{0:0.#}{1}", length, sizes[order]);
            }
            catch(OverflowException) // I'm not sure why I would get an overflow exception, but I did once and so I swallow it here.
            {
                return "...";
            }
        }

        private PushStatus FinishPush(string transactionId)
        {
            var apiResponse = _apiServer.Execute("finishPushBundle", new HgResumeApiParameters { TransId = transactionId, RepoId = _apiServer.ProjectId }, 20);
            _progress.WriteVerbose("API URL: {0}", _apiServer.Url);
            switch (apiResponse.HttpStatus)
            {
                case HttpStatusCode.OK:
                    return PushStatus.Complete;
                case HttpStatusCode.BadRequest:
                    return PushStatus.Fail;
                case HttpStatusCode.ServiceUnavailable:
                    return PushStatus.NotAvailable;
            }
            return PushStatus.Fail;
        }

        private PushResponse PushOneChunk(HgResumeApiParameters request, byte[] dataToPush)
        {
            var pushResponse = new PushResponse(PushStatus.Fail);
            try
            {
                HgResumeApiResponse response = _apiServer.Execute("pushBundleChunk", request, dataToPush, TimeoutInSeconds);
                if (response == null)
                {
                    _progress.WriteVerbose("API REQ: {0} Timeout");
                    pushResponse.Status = PushStatus.Timeout;
                    return pushResponse;
                }
                /* API returns the following HTTP codes:
                 * 200 OK (SUCCESS)
                 * 202 Accepted (RECEIVED)
                 * 412 Precondition Failed (RESEND)
                 * 400 Bad Request (FAIL, UNKNOWNID, and RESET)
                 */
                _progress.WriteVerbose("API REQ: {0} RSP: {1} in {2}ms", _apiServer.Url, response.HttpStatus, response.ResponseTimeInMilliseconds);
                if (response.HttpStatus == HttpStatusCode.ServiceUnavailable && response.Content.Length > 0)
                {
                    var msg = String.Format("Server temporarily unavailable: {0}",
                                            Encoding.UTF8.GetString(response.Content));
                    _progress.WriteError(msg);
                    pushResponse.Status = PushStatus.NotAvailable;
                    return pushResponse;
                }
                if (response.ResumableResponse.HasNote)
                {
                    _progress.WriteWarning(String.Format("Server replied: {0}", response.ResumableResponse.Note));
                }
                // the chunk was received successfully
                if (response.HttpStatus == HttpStatusCode.Accepted)
                {
                    pushResponse.StartOfWindow = response.ResumableResponse.StartOfWindow;
                    pushResponse.Status = PushStatus.Received;
                    pushResponse.ChunkSize = CalculateChunkSize(request.ChunkSize, response.ResponseTimeInMilliseconds);
                    return pushResponse;
                }

                // the final chunk was received successfully
                if (response.HttpStatus == HttpStatusCode.OK)
                {
                    pushResponse.Status = PushStatus.Complete;
                    return pushResponse;
                }

                if (response.HttpStatus == HttpStatusCode.BadRequest)
                {
                    if (response.ResumableResponse.Status == "UNKNOWNID")
                    {
                        _progress.WriteError("The server {0} does not have the project '{1}'", _targetLabel, _apiServer.ProjectId);
                        return pushResponse;
                    }
                    if (response.ResumableResponse.Status == "RESET")
                    {
                        _progress.WriteError("Push failed: All chunks were pushed to the server, but the unbundle operation failed.  Try again later.");
                        pushResponse.Status = PushStatus.Reset;
                        return pushResponse;
                    }
                    if (response.ResumableResponse.HasError)
                    {
                        if (response.ResumableResponse.Error == "invalid baseHash")
                        {
                            pushResponse.Status = PushStatus.InvalidHash;
                        }
                        else
                        {
                            _progress.WriteError("Server Error: {0}", response.ResumableResponse.Error);
                        }
                        return pushResponse;
                    }
                                
                }
                _progress.WriteWarning("Invalid Server Response '{0}'", response.HttpStatus);
                return pushResponse;
            }
            catch (WebException e)
            {
                _progress.WriteWarning(String.Format("Push data chunk failed: {0}", e.Message));
                return pushResponse;
            }
        }

        private static int CalculateChunkSize(int chunkSize, long responseTimeInMilliseconds)
        {
            // just in case the response time is 0 milliseconds
            if (responseTimeInMilliseconds == 0)
            {
                responseTimeInMilliseconds = 1;
            }

            long newChunkSize = TargetTimeInSeconds*1000*chunkSize/responseTimeInMilliseconds;

            if (newChunkSize > MaximumChunkSize)
            {
                newChunkSize = MaximumChunkSize;
            }

            // if the difference between the new chunksize value is less than 10K, don't suggest a new chunkSize, to avoid fluxuations in chunksizes
            if (Math.Abs(chunkSize - newChunkSize) < 10000)
            {
                return chunkSize;
            }
            return (int) newChunkSize;
        }

        public bool Pull()
        {
            /*var baseHashes = GetCommonBaseHashesWithRemoteRepo();
            if (baseHashes == null || baseHashes.Count == 0)
            {
                // a null or empty list indicates that the server has an empty repo
                // in this case there is no reason to Pull
                return false;
            }
			return Pull(GetHashStringsFromRevisions(baseHashes));
             */
            return Pull();
        }

        public bool Pull(string[] baseRevisions)
        {
            var tipRevision = _repo.GetTip();
            string localTip = "0";
            string errorMessage;
            if (tipRevision != null)
            {
                localTip = tipRevision.Number.Hash;
            }
            
            if (baseRevisions.Length == 0)
            {
                errorMessage = "Pull failed: No base revision.";
                _progress.WriteError(errorMessage);
                throw new WeShareOperationFailed(errorMessage);
            }

        	string bundleId = "";
        	foreach (var revision in baseRevisions)
        	{
				bundleId += revision + "_" + localTip + '-';
        	}
        	bundleId = bundleId.TrimEnd('-');
            var bundleHelper = new PullStorageManager(PathToLocalStorage, bundleId);
            var req = new HgResumeApiParameters
                      {
                          RepoId = _apiServer.ProjectId,
                          BaseHashes = baseRevisions,
                          TransId = bundleHelper.TransactionId,
                          StartOfWindow = bundleHelper.StartOfWindow,
                          ChunkSize = InitialChunkSize
                      };
            int bundleSize = 0;

            int loopCtr = 1;
            bool retryLoop;

            do
            {
                if (_progress.CancelRequested)
                {
                    throw new Palaso.CommandLineProcessing.UserCancelledException();
                }
                retryLoop = false;
                var response = PullOneChunk(req);
				if (response.Status == PullStatus.Unauthorized)
				{
					throw new UnauthorizedAccessException();
				}
                if (response.Status == PullStatus.NotAvailable)
                {
                    _progress.ProgressIndicator.Initialize();
                    _progress.ProgressIndicator.Finish();
                    return false;
                }
                if (response.Status == PullStatus.Timeout)
                {
                    _progress.WriteWarning("Pull operation timed out.  Retrying...");
                    retryLoop = true;
                    continue;
                }
                if (response.Status == PullStatus.NoChange)
                {
                    _progress.WriteMessage("No changes");
                    bundleHelper.Cleanup();
                    _progress.ProgressIndicator.Initialize();
                    _progress.ProgressIndicator.Finish();
                    return false;
                }
                if (response.Status == PullStatus.InProgress)
                {
                    _progress.WriteStatus("Preparing data on server");
                    retryLoop = true;
                    // advance the progress bar 2% to show that something is happening
                    _progress.ProgressIndicator.PercentCompleted = _progress.ProgressIndicator.PercentCompleted + 2;
                    continue;
                }
                if (response.Status == PullStatus.InvalidHash)
                {
                    // this should not happen...but sometimes it gets into a state where it remembers the wrong basehash of the server (CJH Feb-12)
                    retryLoop = true;
                    _progress.WriteVerbose("Invalid basehash response received from server... clearing cache and retrying");
                    //req.BaseHashes = GetHashStringsFromRevisions(GetCommonBaseHashesWithRemoteRepo(false));
                    continue;
                }
                if (response.Status == PullStatus.Fail)
                {
                    // Not sure that we need to abort the attempts just because .Net web request says so.  See Push also. CP 2012-06
                    errorMessage = "Pull data chunk failed";
                    _progress.WriteError(errorMessage);
                    retryLoop = true;
                    req.StartOfWindow = bundleHelper.StartOfWindow;
                    //_progress.ProgressIndicator.Initialize();
                    //_progress.ProgressIndicator.Finish();
                    //throw new WeShareOperationFailed(errorMessage);
                    continue;
                }
                if (response.Status == PullStatus.Reset)
                {
                    retryLoop = true;
                    bundleHelper.Reset();
                    _progress.WriteVerbose("Server's bundle cache has expired.  Restarting pull...");
                    req.StartOfWindow = bundleHelper.StartOfWindow;
                    continue;
                }
                
                //bundleSizeFromResponse = response.BundleSize;
                if (loopCtr == 1)
                {
                    _progress.ProgressIndicator.Initialize();
                    if (req.StartOfWindow != 0)
                    {
                        string message = String.Format("Resuming pull operation at {0} received", GetHumanReadableByteSize(req.StartOfWindow));
                        _progress.WriteVerbose(message);
                    }
                }

                bundleHelper.WriteChunk(req.StartOfWindow, response.Chunk);
                req.StartOfWindow = req.StartOfWindow + response.Chunk.Length;
                req.ChunkSize = response.ChunkSize;
                if (bundleSize == response.BundleSize && bundleSize != 0)
                {
                    _progress.ProgressIndicator.PercentCompleted = (int)((long)req.StartOfWindow * 100 / bundleSize);
                    string eta = CalculateEstimatedTimeRemaining(bundleSize, req.ChunkSize, req.StartOfWindow);
                    _progress.WriteStatus(string.Format("Receiving {0} {1}", GetHumanReadableByteSize(bundleSize), eta));
                }
                else
                {
                    // this is only useful when the bundle size is significantly large (like with a clone operation) such that
                    // the server takes a long time to create the bundle, and the bundleSize continues to rise as the chunks are received
                    bundleSize = response.BundleSize;
                    _progress.WriteStatus(string.Format("Preparing data on server (>{0})", GetHumanReadableByteSize(bundleSize)));
                }
                loopCtr++;

            } while (req.StartOfWindow < bundleSize || retryLoop);

            if (_repo.Unbundle(bundleHelper.BundlePath))
            {
                _progress.WriteMessage("Pull operation completed successfully");
                _progress.ProgressIndicator.Finish();
                _progress.WriteMessage("Finished Receiving");
                bundleHelper.Cleanup();
                var response = FinishPull(req.TransId);
                if (response == PullStatus.Reset)
                {
                    /* Calling Pull recursively to finish up another pull will mess up the ProgressIndicator.  This case is
                    // rare enough that it's not worth trying to get the progress indicator working for a recursive Pull()
                    */ 
                    _progress.WriteMessage("Remote repo has changed.  Initiating additional pull operation");
                    return Pull();
                }

                // REVIEW: I'm not sure why this was set to the server tip before, if we just pulled then won't our head
				// be the correct common base? Maybe not if a merge needs to happen,
                //LastKnownCommonBases = new List<Revision>(_repo.BranchingHelper.GetBranches());
                return true;
            }
            _progress.WriteError("Received all data but local unbundle operation failed or resulted in multiple heads!");
            _progress.ProgressIndicator.Finish();
            bundleHelper.Cleanup();
            errorMessage = "Pull operation failed";
            _progress.WriteError(errorMessage);
            throw new WeShareOperationFailed(errorMessage);
        }

    	internal static string[] GetHashStringsFromRevisions(IEnumerable<Revision> branchHeadRevisions)
    	{
    		var hashes = new string[branchHeadRevisions.Count()];
			for(var index = 0; index < branchHeadRevisions.Count(); ++index)
			{
				hashes[index] = branchHeadRevisions.ElementAt(index).Number.Hash;
			}
			return hashes;
    	}

        private PullStatus FinishPull(string transactionId)
        {
            var apiResponse = _apiServer.Execute("finishPullBundle", new HgResumeApiParameters {TransId  = transactionId, RepoId = _apiServer.ProjectId }, 20);
            switch (apiResponse.HttpStatus)
            {
                case HttpStatusCode.OK:
                    return PullStatus.OK;
                case HttpStatusCode.BadRequest:
                    if (apiResponse.ResumableResponse.Status == "RESET")
                    {
                        return PullStatus.Reset;
                    }
                    return PullStatus.Fail;
                case HttpStatusCode.ServiceUnavailable:
                    return PullStatus.NotAvailable;
            }
            return PullStatus.Fail;
        }

		private PullResponse PullOneChunk(HgResumeApiParameters request)
        {
            var pullResponse = new PullResponse(PullStatus.Fail);
            try
            {

                HgResumeApiResponse response = _apiServer.Execute("pullBundleChunk", request, TimeoutInSeconds);
				if (response == null)
                {
                    _progress.WriteVerbose("API REQ: {0} Timeout", _apiServer.Url);
                    pullResponse.Status = PullStatus.Timeout;
                    return pullResponse;
                }
                /* API returns the following HTTP codes:
                 * 200 OK (SUCCESS)
                 * 304 Not Modified (NOCHANGE)
                 * 400 Bad Request (FAIL, UNKNOWNID)
                 */
                _progress.WriteVerbose("API REQ: {0} RSP: {1} in {2}ms", _apiServer.Url, response.HttpStatus, response.ResponseTimeInMilliseconds);
                if (response.ResumableResponse.HasNote)
                {
                    _progress.WriteMessage(String.Format("Server replied: {0}", response.ResumableResponse.Note));
                }

                if (response.HttpStatus == HttpStatusCode.ServiceUnavailable && response.Content.Length > 0)
                {
                    var msg = String.Format("Server temporarily unavailable: {0}",
                                            Encoding.UTF8.GetString(response.Content));
                    _progress.WriteError(msg);
                    pullResponse.Status = PullStatus.NotAvailable;
                    return pullResponse;
                }
                if (response.HttpStatus == HttpStatusCode.NotModified)
                {
                    pullResponse.Status = PullStatus.NoChange;
                    return pullResponse;
                }
                if (response.HttpStatus == HttpStatusCode.Accepted)
                {
                    pullResponse.Status = PullStatus.InProgress;
                    return pullResponse;
                }

                // chunk pulled OK
                if (response.HttpStatus == HttpStatusCode.OK)
                {
                    pullResponse.BundleSize = response.ResumableResponse.BundleSize;
                    pullResponse.Status = PullStatus.OK;
                    pullResponse.ChunkSize = CalculateChunkSize(request.ChunkSize, response.ResponseTimeInMilliseconds);

                    pullResponse.Chunk = response.Content;
                    return pullResponse;
                }
                if (response.HttpStatus == HttpStatusCode.BadRequest && response.ResumableResponse.Status == "UNKNOWNID")
                {
                    // this is not implemented currently (feb 2012 cjh)
                    _progress.WriteError("The server {0} does not have the project '{1}'", _targetLabel, request.RepoId);
                    return pullResponse;
                }
                if (response.HttpStatus == HttpStatusCode.BadRequest && response.ResumableResponse.Status == "RESET")
                {
                    pullResponse.Status = PullStatus.Reset;
                    return pullResponse;
                }
                if (response.HttpStatus == HttpStatusCode.BadRequest)
                {
                    if (response.ResumableResponse.HasError)
                    {
                        if (response.ResumableResponse.Error == "invalid baseHash")
                        {
                            pullResponse.Status = PullStatus.InvalidHash;
                        } else
                        {
                            _progress.WriteWarning("Server Error: {0}", response.ResumableResponse.Error);
                        }
                    }
                    return pullResponse;
                }
				if (response.HttpStatus == HttpStatusCode.Unauthorized)
				{
					_progress.WriteWarning("There is an authorization problem accessing this project. Check the project ID as well as your username and password. Alternatively, you may not be authorized to access this project.");
					pullResponse.Status =  PullStatus.Unauthorized;
				}
                _progress.WriteWarning("Invalid Server Response '{0}'", response.HttpStatus);
                return pullResponse;
            }
            catch (WebException e)
            {
                _progress.WriteWarning(String.Format("Pull data chunk failed: {0}", e.Message));
                return pullResponse;
            }
        }

        ///<summary>
        /// returns something like %AppData%\Chorus\ChorusStorage\uniqueRepoId
        ///</summary>
        public string PathToLocalStorage
        {
            get
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chorus");
                return Path.Combine(appDataPath,
                                    Path.Combine("ChorusStorage",
                                                 RepoIdentifier)
                    );
            }
        }

        public void Clone()
        {
            if (!_repo.IsInitialized)
            {
                _repo.Init();
            }
            try
            {
            	Pull(new []{"0"});
            }
            catch(WeShareOperationFailed)
            {
                throw new WeShareOperationFailed("Clone operation failed");
            }
        }

        public void RemoveCache()
        {
            var localStoragePath = PathToLocalStorage;
            if (Directory.Exists(localStoragePath))
            {
                Directory.Delete(localStoragePath, true);
            }
        }
    }

    public class WeShareFile
    {
        public String Path;
        public String MD5;
        public DateTime ModifiedDate;
        public FileInfo FileInfo;
    }
}