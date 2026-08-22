using System;
using System.Windows;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Stammdaten einer Liegenschaft. Das Fenster arbeitet auf einer Arbeitskopie:
/// erst beim Speichern werden die Werte in das Dossier uebernommen, damit ein
/// Abbrechen wirklich nichts veraendert.
/// </summary>
public partial class DossierEditWindow : Window
{
    private readonly DossierDefinition _target;

    private DossierEditWindow(DossierDefinition target, bool isNew)
    {
        InitializeComponent();
        _target = target;

        Title = isNew ? "Neue Liegenschaft" : "Liegenschaft bearbeiten";
        Load();
    }

    /// <summary>Zeigt das Fenster. Liefert true, wenn gespeichert wurde.</summary>
    public static bool ShowFor(DossierDefinition definition, bool isNew)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var window = new DossierEditWindow(definition, isNew)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return window.ShowDialog() == true;
    }

    private void Load()
    {
        NameBox.Text = _target.Name;
        ParcelBox.Text = _target.ParcelNumbers;
        HouseBox.Text = _target.HouseNumbers;
        AddressBox.Text = _target.Address;
        ZipBox.Text = _target.PostalCode;
        TownBox.Text = _target.Town;

        OwnerBox.Text = _target.OwnerName;
        OwnerAddressBox.Text = _target.OwnerAddress;
        ContactBox.Text = _target.ContactName;
        PhoneBox.Text = _target.ContactPhone;
        MailBox.Text = _target.ContactMail;
        OccupancyBox.Text = _target.Occupancy;

        ProcessBox.Text = _target.ConstructionProcess;
        RemarksBox.Text = _target.Remarks;
        AttachmentsBox.Text = string.IsNullOrWhiteSpace(_target.Attachments)
            ? DefaultAttachmentList
            : _target.Attachments;
        RevisionBox.Text = _target.Revision;

        ExecutionOverrideBox.Text = _target.ExecutionDateOverride ?? "";
        ContactPersonOverrideBox.Text = _target.ContactPersonOverride ?? "";
        ContractorOverrideBox.Text = _target.ContractorOverride ?? "";
        SiteManagementOverrideBox.Text = _target.SiteManagementOverride ?? "";
        ObstructionsOverrideBox.Text = _target.ObstructionsOverride ?? "";
        HouseConnectionOverrideBox.Text = _target.HouseConnectionTextOverride ?? "";
        StormWaterOverrideBox.Text = _target.StormWaterTextOverride ?? "";
        DeadlineOverrideBox.Text = _target.ResponseDeadlineOverride ?? "";
    }

    private const string DefaultAttachmentList =
        "Situation Liegenschaft GIS\n"
        + "Situation Abwasserleitungen der TV-Aufnahmen\n"
        + "TV-Haltungsprotokolle\n"
        + "Offerte";

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim() ?? "";
        if (name.Length == 0)
        {
            // Ohne Namen gaebe es keinen sinnvollen Ordner- und Listeneintrag.
            HintText.Text = "Bitte einen Namen eingeben.";
            NameBox.Focus();
            return;
        }

        _target.Name = name;
        _target.ParcelNumbers = Trim(ParcelBox.Text);
        _target.HouseNumbers = Trim(HouseBox.Text);
        _target.Address = Trim(AddressBox.Text);
        _target.PostalCode = Trim(ZipBox.Text);
        _target.Town = Trim(TownBox.Text);

        _target.OwnerName = Trim(OwnerBox.Text);
        _target.OwnerAddress = Trim(OwnerAddressBox.Text);
        _target.ContactName = Trim(ContactBox.Text);
        _target.ContactPhone = Trim(PhoneBox.Text);
        _target.ContactMail = Trim(MailBox.Text);
        _target.Occupancy = Trim(OccupancyBox.Text);

        _target.ConstructionProcess = Trim(ProcessBox.Text);
        _target.Remarks = Trim(RemarksBox.Text);
        _target.Attachments = Trim(AttachmentsBox.Text);

        var revision = Trim(RevisionBox.Text);
        _target.Revision = revision.Length == 0 ? "A" : revision;

        _target.ExecutionDateOverride = NullIfEmpty(ExecutionOverrideBox.Text);
        _target.ContactPersonOverride = NullIfEmpty(ContactPersonOverrideBox.Text);
        _target.ContractorOverride = NullIfEmpty(ContractorOverrideBox.Text);
        _target.SiteManagementOverride = NullIfEmpty(SiteManagementOverrideBox.Text);
        _target.ObstructionsOverride = NullIfEmpty(ObstructionsOverrideBox.Text);
        _target.HouseConnectionTextOverride = NullIfEmpty(HouseConnectionOverrideBox.Text);
        _target.StormWaterTextOverride = NullIfEmpty(StormWaterOverrideBox.Text);
        _target.ResponseDeadlineOverride = NullIfEmpty(DeadlineOverrideBox.Text);

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private static string Trim(string? value) => value?.Trim() ?? "";

    private static string? NullIfEmpty(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        return trimmed.Length == 0 ? null : trimmed;
    }
}
