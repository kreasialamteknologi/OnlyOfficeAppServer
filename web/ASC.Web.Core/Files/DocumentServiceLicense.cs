/*
 *
 * (c) Copyright Ascensio System Limited 2010-2021
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.Collections.Generic;

using ASC.Common;
using ASC.Common.Caching;
using ASC.Core;
using ASC.Core.Billing;

using static ASC.Web.Core.Files.DocumentService;

namespace ASC.Web.Core.Files;

[Scope]
public class DocumentServiceLicense
{
    private static readonly TimeSpan CACHE_EXPIRATION = TimeSpan.FromMinutes(15);

    private readonly ICache _cache;
    private readonly CoreBaseSettings _coreBaseSettings;
    private readonly FilesLinkUtility _filesLinkUtility;
    private readonly FileUtility _fileUtility;

    public DocumentServiceLicense(
        ICache cache,
        CoreBaseSettings coreBaseSettings,
        FilesLinkUtility filesLinkUtility,
        FileUtility fileUtility)
    {
        _cache = cache;
        _coreBaseSettings = coreBaseSettings;
        _filesLinkUtility = filesLinkUtility;
        _fileUtility = fileUtility;
    }

    private CommandResponse GetDocumentServiceLicense()
    {
        if (!_coreBaseSettings.Standalone) return null;
        if (string.IsNullOrEmpty(_filesLinkUtility.DocServiceCommandUrl)) return null;

        var cacheKey = "DocumentServiceLicense";
        var commandResponse = _cache.Get<CommandResponse>(cacheKey);
        if (commandResponse == null)
        {
            commandResponse = DocumentService.CommandRequest(
                    _fileUtility,
                    _filesLinkUtility.DocServiceCommandUrl,
                    DocumentService.CommandMethod.License,
                    null,
                    null,
                    null,
                    null,
                    _fileUtility.SignatureSecret);
            _cache.Insert(cacheKey, commandResponse, DateTime.UtcNow.Add(CACHE_EXPIRATION));
        }

        return commandResponse;
    }

    public Dictionary<string, DateTime> GetLicenseQuota()
    {
        var commandResponse = GetDocumentServiceLicense();
        if (commandResponse == null
            || commandResponse.Quota == null
            || commandResponse.Quota.Users == null)
            return null;

        var result = new Dictionary<string, DateTime>();
        commandResponse.Quota.Users.ForEach(user => result.Add(user.UserId, user.Expire));
        return result;
    }

    public License GetLicense()
    {
        var commandResponse = GetDocumentServiceLicense();
        if (commandResponse == null)
            return null;

        return commandResponse.License;
    }
}