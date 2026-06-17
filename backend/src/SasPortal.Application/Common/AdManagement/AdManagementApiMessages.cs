namespace SasPortal.Application.Common.AdManagement;

public static class AdManagementApiMessages
{
    public static string Legacy(
        string messageKey,
        IReadOnlyDictionary<string, object>? messageParams = null)
    {
        _ = messageParams;
        return messageKey switch
        {
            Constants.AdManagementApiMessageKeys.Common.NotConfigured =>
                "AD yönetim ayarları yapılandırılmamış. Lütfen önce bağlantı ayarlarını kaydedin.",
            Constants.AdManagementApiMessageKeys.Common.ModuleDisabled =>
                "AD yönetim modülü etkin değil.",
            Constants.AdManagementApiMessageKeys.Common.MissingServiceAccountPassword =>
                "AD yönetim servis hesabı parolası tanımlı değil.",
            Constants.AdManagementApiMessageKeys.Common.ConnectionFailed =>
                "AD bağlantısı kurulamadı.",
            Constants.AdManagementApiMessageKeys.Common.LdapsRequired =>
                AdDirectoryConnectionRequirements.LdapsRequiredMessage,

            Constants.AdManagementApiMessageKeys.SettingsValidation.MissingRequiredSettings =>
                "AD yönetim ayarları için zorunlu alanlar eksik.",
            Constants.AdManagementApiMessageKeys.SettingsValidation.ServiceAccountBindFailed =>
                "AD yönetim servis hesabı ile bağlantı kurulamadı. NetBIOS domain adı, servis hesabı kullanıcı adı veya parola hatalı olabilir.",
            Constants.AdManagementApiMessageKeys.SettingsValidation.DomainFqdnUnreachable =>
                "Domain FQDN erişilemedi veya doğrulanamadı.",
            Constants.AdManagementApiMessageKeys.SettingsValidation.BaseDnNotResolved =>
                "Base DN çözümlenemedi.",
            Constants.AdManagementApiMessageKeys.SettingsValidation.DefaultNamingContextNotResolved =>
                "Default naming context çözümlenemedi.",
            Constants.AdManagementApiMessageKeys.SettingsValidation.UsersRootOuNotResolved =>
                "Users root OU çözümlenemedi.",
            Constants.AdManagementApiMessageKeys.SettingsValidation.DisabledUsersOuNotResolved =>
                "Pasif kullanıcılar OU çözümlenemedi.",
            Constants.AdManagementApiMessageKeys.SettingsValidation.GroupsSearchBaseNotResolved =>
                "Gruplar arama base çözümlenemedi.",
            Constants.AdManagementApiMessageKeys.SettingsValidation.ComputersSearchBaseNotResolved =>
                "Bilgisayarlar arama base çözümlenemedi.",
            Constants.AdManagementApiMessageKeys.SettingsValidation.PreferredDcUnreachable =>
                "Tercih edilen DC erişilemedi.",
            Constants.AdManagementApiMessageKeys.SettingsValidation.ValidationSucceeded =>
                "AD yönetim ayarları doğrulandı.",

            Constants.AdManagementApiMessageKeys.Users.NotFound =>
                "AD kullanıcısı bulunamadı.",
            Constants.AdManagementApiMessageKeys.Users.QueryFailed =>
                "AD kullanıcıları okunamadı.",
            Constants.AdManagementApiMessageKeys.Users.CreateSuccess =>
                "AD kullanıcısı oluşturuldu.",
            Constants.AdManagementApiMessageKeys.Users.CreateFailed =>
                "AD kullanıcısı oluşturulamadı.",
            Constants.AdManagementApiMessageKeys.Users.UpdateFailed =>
                "AD kullanıcısı güncellenemedi.",
            Constants.AdManagementApiMessageKeys.Users.OuMoveSuccess =>
                "Kullanıcı seçilen OU'ya taşındı.",
            Constants.AdManagementApiMessageKeys.Users.OuMoveFailed =>
                "OU taşıma işlemi başarısız oldu.",
            Constants.AdManagementApiMessageKeys.Users.TargetOuRequired =>
                "Hedef OU seçimi zorunludur.",
            Constants.AdManagementApiMessageKeys.Users.AlreadyInTargetOu =>
                "Kullanıcı zaten seçilen OU içinde.",
            Constants.AdManagementApiMessageKeys.Users.InvalidTargetOu =>
                "Seçilen OU, AD yönetim ayarlarındaki kullanıcı kök OU altında olmalıdır.",
            Constants.AdManagementApiMessageKeys.Users.NamingConflictFailed =>
                "Uygun kullanıcı adı veya UPN bulunamadı. Lütfen farklı bilgiler deneyin.",
            Constants.AdManagementApiMessageKeys.Users.MissingUpnSuffix =>
                "UPN suffix seçimi zorunludur.",
            Constants.AdManagementApiMessageKeys.Users.InvalidUpnSuffix =>
                "UPN suffix geçerli bir domain suffix olmalıdır.",
            Constants.AdManagementApiMessageKeys.Users.ManagerUpdateFailed =>
                "Manager güncellenemedi.",
            Constants.AdManagementApiMessageKeys.Users.ManagerSelfSelection =>
                "Kullanıcı kendisinin manager'ı olamaz.",
            Constants.AdManagementApiMessageKeys.Users.ManagerNotFound =>
                "Seçilen manager kullanıcısı bulunamadı.",
            Constants.AdManagementApiMessageKeys.Users.AccountExpirationUpdateFailed =>
                "Hesap bitiş tarihi güncellenemedi.",
            Constants.AdManagementApiMessageKeys.Users.AccountExpirationInvalidDate =>
                "Hesap bitiş tarihi geçersiz.",
            Constants.AdManagementApiMessageKeys.Users.AccountOperationFailed =>
                "Kullanıcı hesabı işlemi başarısız oldu.",
            Constants.AdManagementApiMessageKeys.Users.AccountEnabled =>
                "Kullanıcı hesabı etkinleştirildi.",
            Constants.AdManagementApiMessageKeys.Users.AccountDisabled =>
                "Kullanıcı hesabı devre dışı bırakıldı.",
            Constants.AdManagementApiMessageKeys.Users.AccountUnlocked =>
                "Kullanıcı hesabının kilidi açıldı.",
            Constants.AdManagementApiMessageKeys.Users.AccountAlreadyEnabled =>
                "Kullanıcı hesabı zaten etkin.",
            Constants.AdManagementApiMessageKeys.Users.AccountAlreadyDisabled =>
                "Kullanıcı hesabı zaten devre dışı.",
            Constants.AdManagementApiMessageKeys.Users.AccountNotLocked =>
                "Kullanıcı hesabı kilitli değil.",
            Constants.AdManagementApiMessageKeys.Users.GroupOperationFailed =>
                "Grup üyeliği işlemi başarısız oldu.",
            Constants.AdManagementApiMessageKeys.Users.GroupMembershipAdded =>
                "Kullanıcı gruba eklendi.",
            Constants.AdManagementApiMessageKeys.Users.GroupMembershipRemoved =>
                "Kullanıcı gruptan çıkarıldı.",
            Constants.AdManagementApiMessageKeys.Users.AlreadyInGroup =>
                "Kullanıcı bu gruba zaten üye.",
            Constants.AdManagementApiMessageKeys.Users.NotInGroup =>
                "Kullanıcı bu grupta değil.",
            Constants.AdManagementApiMessageKeys.Users.EffectiveGroupsFailed =>
                "Etkin grup üyelikleri okunamadı.",
            Constants.AdManagementApiMessageKeys.Users.InvalidUserId =>
                "Geçersiz kullanıcı kimliği.",

            Constants.AdManagementApiMessageKeys.Groups.NotFound =>
                "AD grubu bulunamadı.",
            Constants.AdManagementApiMessageKeys.Groups.QueryFailed =>
                "AD grupları okunamadı.",
            Constants.AdManagementApiMessageKeys.Groups.CreateSuccess =>
                "AD grubu oluşturuldu.",
            Constants.AdManagementApiMessageKeys.Groups.CreateFailed =>
                "AD grubu oluşturulamadı.",
            Constants.AdManagementApiMessageKeys.Groups.UpdateFailed =>
                "AD grubu güncellenemedi.",
            Constants.AdManagementApiMessageKeys.Groups.DeleteSuccess =>
                "AD grubu silindi.",
            Constants.AdManagementApiMessageKeys.Groups.DeleteFailed =>
                "AD grubu silinemedi.",
            Constants.AdManagementApiMessageKeys.Groups.OuMoveSuccess =>
                "Grup seçilen OU'ya taşındı.",
            Constants.AdManagementApiMessageKeys.Groups.OuMoveFailed =>
                "Grup OU taşıma işlemi başarısız oldu.",
            Constants.AdManagementApiMessageKeys.Groups.TargetOuRequired =>
                "Hedef OU seçimi zorunludur.",
            Constants.AdManagementApiMessageKeys.Groups.AlreadyInTargetOu =>
                "Grup zaten seçilen OU içinde.",
            Constants.AdManagementApiMessageKeys.Groups.MemberOperationFailed =>
                "Grup üyeliği işlemi başarısız oldu.",
            Constants.AdManagementApiMessageKeys.Groups.MemberAdded =>
                "Üye gruba eklendi.",
            Constants.AdManagementApiMessageKeys.Groups.MemberRemoved =>
                "Üye gruptan çıkarıldı.",
            Constants.AdManagementApiMessageKeys.Groups.MemberAlreadyInGroup =>
                "Üye bu gruba zaten doğrudan üye.",
            Constants.AdManagementApiMessageKeys.Groups.MemberNotInGroup =>
                "Üye bu grupta doğrudan üye değil.",
            Constants.AdManagementApiMessageKeys.Groups.SelfMembership =>
                "Grup kendisine üye yapılamaz.",
            Constants.AdManagementApiMessageKeys.Groups.InvalidGroupId =>
                "Geçersiz grup kimliği.",
            Constants.AdManagementApiMessageKeys.Groups.GroupDnRequired =>
                "Grup kimliği zorunludur.",

            Constants.AdManagementApiMessageKeys.Computers.NotFound =>
                "AD bilgisayarı bulunamadı.",
            Constants.AdManagementApiMessageKeys.Computers.QueryFailed =>
                "AD bilgisayarları okunamadı.",
            Constants.AdManagementApiMessageKeys.Computers.UpdateFailed =>
                "Bilgisayar açıklaması güncellenemedi.",
            Constants.AdManagementApiMessageKeys.Computers.DeleteSuccess =>
                "Bilgisayar hesabı silindi.",
            Constants.AdManagementApiMessageKeys.Computers.DeleteFailed =>
                "Bilgisayar hesabı silinemedi.",
            Constants.AdManagementApiMessageKeys.Computers.OuMoveSuccess =>
                "Bilgisayar OU taşıma işlemi tamamlandı.",
            Constants.AdManagementApiMessageKeys.Computers.OuMoveFailed =>
                "Bilgisayar OU taşıma işlemi başarısız oldu.",
            Constants.AdManagementApiMessageKeys.Computers.TargetOuRequired =>
                "Hedef OU seçimi zorunludur.",
            Constants.AdManagementApiMessageKeys.Computers.AlreadyInTargetOu =>
                "Bilgisayar zaten seçilen OU içinde.",
            Constants.AdManagementApiMessageKeys.Computers.AccountOperationFailed =>
                "Bilgisayar hesabı işlemi başarısız oldu.",
            Constants.AdManagementApiMessageKeys.Computers.AccountEnabled =>
                "Bilgisayar hesabı etkinleştirildi.",
            Constants.AdManagementApiMessageKeys.Computers.AccountDisabled =>
                "Bilgisayar hesabı devre dışı bırakıldı.",
            Constants.AdManagementApiMessageKeys.Computers.GroupOperationFailed =>
                "Grup üyeliği işlemi başarısız oldu.",
            Constants.AdManagementApiMessageKeys.Computers.GroupMembershipAdded =>
                "Bilgisayar gruba eklendi.",
            Constants.AdManagementApiMessageKeys.Computers.GroupMembershipRemoved =>
                "Bilgisayar gruptan çıkarıldı.",
            Constants.AdManagementApiMessageKeys.Computers.AlreadyInGroup =>
                "Bilgisayar bu gruba zaten üye.",
            Constants.AdManagementApiMessageKeys.Computers.NotInGroup =>
                "Bilgisayar bu grupta değil.",
            Constants.AdManagementApiMessageKeys.Computers.ProtectedDelete =>
                AdComputerAccountGuard.ProtectedComputerDeleteMessage,
            Constants.AdManagementApiMessageKeys.Computers.ProtectedWrite =>
                AdComputerAccountGuard.ProtectedComputerWriteOperationMessage,
            Constants.AdManagementApiMessageKeys.Computers.InvalidComputerId =>
                "Geçersiz bilgisayar kimliği.",

            Constants.AdManagementApiMessageKeys.DeletedObjects.NotFound =>
                "Silinen AD nesnesi bulunamadı.",
            Constants.AdManagementApiMessageKeys.DeletedObjects.QueryFailed =>
                "Silinen AD nesneleri okunamadı.",
            Constants.AdManagementApiMessageKeys.DeletedObjects.AccessDenied =>
                "Silinen nesneler listesine erişim reddedildi.",
            Constants.AdManagementApiMessageKeys.DeletedObjects.RestoreSuccess =>
                "Silinen nesne geri yüklendi.",
            Constants.AdManagementApiMessageKeys.DeletedObjects.RestoreFailed =>
                "Silinen nesne geri yüklenemedi.",
            Constants.AdManagementApiMessageKeys.DeletedObjects.RestoreUnsupportedType =>
                "Bu nesne türü geri yüklenemez.",
            Constants.AdManagementApiMessageKeys.DeletedObjects.RestoreMissingTarget =>
                "Geri yükleme için son bilinen konum bilgisi bulunamadı.",
            Constants.AdManagementApiMessageKeys.DeletedObjects.RestoreTargetNotFound =>
                "Geri yükleme hedefi bulunamadı.",
            Constants.AdManagementApiMessageKeys.DeletedObjects.RestoreConflict =>
                "Hedef konumda aynı ada sahip bir nesne bulunuyor.",
            Constants.AdManagementApiMessageKeys.DeletedObjects.RestorePowerShellModuleMissing =>
                "Geri yükleme için ActiveDirectory PowerShell modülü gerekli.",
            Constants.AdManagementApiMessageKeys.DeletedObjects.RestoreParentNotFound =>
                "Geri yükleme hedefi bulunamadı.",
            Constants.AdManagementApiMessageKeys.DeletedObjects.RestoreTargetOuNotFound =>
                "Geri yükleme hedef OU bulunamadı.",

            Constants.AdManagementApiMessageKeys.OperationFailures.PreflightSamAccountNameDuplicate =>
                "Bu kullanıcı adı başka bir AD nesnesi tarafından kullanılıyor.",
            Constants.AdManagementApiMessageKeys.OperationFailures.PreflightUpnDuplicate =>
                "Bu UPN başka bir AD nesnesi tarafından kullanılıyor.",
            Constants.AdManagementApiMessageKeys.OperationFailures.PreflightCnDuplicate =>
                "Bu görünen ad/CN aynı OU içinde başka bir AD nesnesi tarafından kullanılıyor.",
            Constants.AdManagementApiMessageKeys.OperationFailures.PreflightGroupSamAccountNameDuplicate =>
                "Aynı sAMAccountName başka bir grup tarafından kullanılıyor.",
            Constants.AdManagementApiMessageKeys.OperationFailures.PreflightGroupCnDuplicate =>
                "Aynı teknik ada sahip bir grup zaten var.",

            Constants.AdManagementApiMessageKeys.Ldap.EntryAlreadyExists =>
                AdLdapErrorNormalizer.EntryAlreadyExistsMessage,
            Constants.AdManagementApiMessageKeys.Ldap.ConstraintViolation =>
                AdLdapErrorNormalizer.ConstraintViolationMessage,
            Constants.AdManagementApiMessageKeys.Ldap.InvalidDnSyntax =>
                AdLdapErrorNormalizer.InvalidDnSyntaxMessage,
            Constants.AdManagementApiMessageKeys.Ldap.InsufficientAccessRights =>
                AdLdapErrorNormalizer.InsufficientAccessRightsMessage,
            Constants.AdManagementApiMessageKeys.Ldap.UnwillingToPerform =>
                AdLdapErrorNormalizer.UnwillingToPerformMessage,
            Constants.AdManagementApiMessageKeys.Ldap.NoSuchObject =>
                AdLdapErrorNormalizer.NoSuchObjectMessage,
            Constants.AdManagementApiMessageKeys.Ldap.ConnectionFailed =>
                AdLdapErrorNormalizer.ConnectionFailedMessage,
            Constants.AdManagementApiMessageKeys.Ldap.UpdateUserFailed =>
                AdLdapErrorNormalizer.UpdateUserFailedMessage,
            Constants.AdManagementApiMessageKeys.Ldap.UpdateGroupFailed =>
                AdLdapErrorNormalizer.UpdateGroupFailedMessage,
            Constants.AdManagementApiMessageKeys.Ldap.CreateGroupFailed =>
                AdLdapErrorNormalizer.CreateGroupFailedMessage,
            Constants.AdManagementApiMessageKeys.Ldap.DeleteGroupFailed =>
                AdLdapErrorNormalizer.DeleteGroupFailedMessage,
            Constants.AdManagementApiMessageKeys.Ldap.GroupNotFound =>
                AdLdapErrorNormalizer.GroupNotFoundMessage,

            _ => messageKey,
        };
    }

    public static (string Message, string MessageKey, IReadOnlyDictionary<string, object>? MessageParams) Bundle(
        string messageKey,
        IReadOnlyDictionary<string, object>? messageParams = null) =>
        (Legacy(messageKey, messageParams), messageKey, messageParams);
}
