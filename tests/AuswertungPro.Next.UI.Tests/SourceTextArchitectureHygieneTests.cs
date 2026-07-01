using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SourceTextArchitectureHygieneTests
{
    [Fact]
    public void Layer_boundary_fitness_tests_live_in_fitness_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var fitness = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "ArchitectureFitnessTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        const string layerBoundaryTest = "PlayerWindow_partials_do_not_import_ui_services_namespace";

        Assert.Contains($"public void {layerBoundaryTest}", fitness);
        Assert.DoesNotContain($"public void {layerBoundaryTest}", guard);
    }

    [Fact]
    public void Schaechte_architecture_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "SchaechtePageArchitectureGuardTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var methodNames = new[]
        {
            "SchaechtePage_dropdown_option_groups_live_in_controller",
            "SchaechtePage_dropdown_record_sync_lives_in_synchronizer",
            "SchaechtePage_template_column_reading_lives_in_infrastructure",
            "SchaechtePage_search_and_nr_logic_uses_application_field_logic"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_controller_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowControllerArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var methodNames = new[]
        {
            "PlayerWindow_damage_markers_live_in_controller",
            "PlayerWindow_quickscan_lives_in_controller"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_wiring_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowWiringArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        const string methodName = "PlayerWindow_constructor_wiring_lives_in_wiring_partial";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_core_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCoreArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var methodNames = new[]
        {
            "PlayerWindow_video_path_validation_lives_in_guard",
            "PlayerWindow_state_fields_live_in_state_partial"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_runtime_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowRuntimeArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var methodNames = new[]
        {
            "PlayerWindow_detection_confirmation_buffer_owns_pending_detection_state",
            "PlayerWindow_service_provider_access_lives_behind_dependencies"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_coding_state_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingStateArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var methodNames = new[]
        {
            "PlayerWindow_coding_state_fields_live_in_coding_state_partial",
            "PlayerWindow_eingabemarker_state_lives_in_state_controller"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_visual_infrastructure_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowVisualInfrastructureArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var methodNames = new[]
        {
            "PlayerWindow_does_not_own_win32_screenshot_capture",
            "PlayerWindow_uses_overlay_tag_constants_for_bend_marker",
            "PlayerWindow_uses_status_color_constants"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_media_infrastructure_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowMediaInfrastructureArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var methodNames = new[]
        {
            "PlayerWindow_slider_track_bounds_live_in_policy",
            "PlayerWindow_libvlc_creation_lives_in_factory"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_playback_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowPlaybackArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var methodNames = new[]
        {
            "PlayerWindow_playback_preview_lives_in_policy_and_speed_controls_in_controller",
            "PlayerWindow_playback_controls_live_in_controls_partial",
            "PlayerWindow_playback_timeline_reads_through_timeline_host",
            "PlayerWindow_keyboard_slider_and_button_playback_uses_control_host",
            "PlayerWindow_playback_rate_uses_control_host",
            "PlayerWindow_playback_start_uses_control_host",
            "Playback_position_fallback_uses_timeline_host",
            "PlayerWindow_snapshot_pause_uses_playback_control_host"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_timer_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowTimerArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var methodNames = new[]
        {
            "PlayerWindow_timer_creation_uses_factory",
            "PlayerWindow_timer_shutdown_uses_stopper"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_coding_statistics_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingStatisticsArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        const string methodName = "PlayerWindow_coding_statistics_live_in_policy";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_protocol_event_mapping_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowProtocolEventMappingArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var methodNames = new[]
        {
            "PlayerWindow_green_protocol_training_candidates_use_resolver",
            "PlayerWindow_existing_protocol_entries_use_mapper",
            "PlayerWindow_import_protocol_events_use_mapper"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_protocol_pdf_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowProtocolPdfArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        const string methodName = "PlayerWindow_coding_pdf_export_uses_planner";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_primary_damage_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowPrimaryDamageArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var methodNames = new[]
        {
            "PlayerWindow_coding_primary_damage_text_uses_existing_mapper",
            "PlayerWindow_primary_damage_text_lives_in_policy"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_shell_project_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowShellProjectArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        const string methodName = "PlayerWindow_shell_project_access_uses_service";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_inline_evidence_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowInlineEvidenceArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        const string methodName = "PlayerWindow_inline_evidence_preview_uses_service";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_stretch_damage_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowStretchDamageArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        const string methodName = "PlayerWindow_open_stretch_damage_prompt_lives_in_policy";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_overlay_input_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowOverlayInputArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var methodNames = new[]
        {
            "PlayerWindow_overlay_measurement_panel_uses_formatter_state",
            "PlayerWindow_overlay_input_mouseflow_keeps_only_direct_dependencies"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_live_detection_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowLiveDetectionArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var methodNames = new[]
        {
            "PlayerWindow_live_detection_status_lives_in_status_partial",
            "PlayerWindow_live_detection_lifecycle_lives_in_lifecycle_partial"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Theory]
    [InlineData("BuilderPageHoldingDataLineBuilderTests.cs")]
    [InlineData("BuilderPagePdfBlockBuilderTests.cs")]
    [InlineData("BuilderPageRowFilterTests.cs")]
    [InlineData("BuilderPageSpecialStatsCalculatorTests.cs")]
    [InlineData("BuilderPageSummaryEntryBuilderTests.cs")]
    [InlineData("BuilderPageViewModelThreadingTests.cs")]
    [InlineData("AiStartupUiTests.cs")]
    [InlineData("ArchitectureFitnessTests.cs")]
    [InlineData("CostCalculatorCatalogFilterArchitectureTests.cs")]
    [InlineData("CostCalculatorImportDefaultsArchitectureTests.cs")]
    [InlineData("CostCalculatorLineOrderArchitectureTests.cs")]
    [InlineData("CostCalculatorLineSuggestionArchitectureTests.cs")]
    [InlineData("CostCalculatorMeasureInputArchitectureTests.cs")]
    [InlineData("CostCalculatorMeasureSelectionArchitectureTests.cs")]
    [InlineData("CostCalculatorPdfExportModelBuilderTests.cs")]
    [InlineData("DataPageAutoSaveArchitectureTests.cs")]
    [InlineData("DataPageCommandTargetControllerTests.cs")]
    [InlineData("DataPageCostRestoreArchitectureTests.cs")]
    [InlineData("DataPageDragStartPolicyTests.cs")]
    [InlineData("DataPageDropReorderControllerTests.cs")]
    [InlineData("DataPageMediaSearchArchitectureTests.cs")]
    [InlineData("DataPageMeasureSuggestionArchitectureTests.cs")]
    [InlineData("DataPageOriginalPdfArchitectureTests.cs")]
    [InlineData("DataPagePrintArchitectureTests.cs")]
    [InlineData("DataPageProtocolMediaLinkArchitectureTests.cs")]
    [InlineData("DataPageProtocolWindowArchitectureTests.cs")]
    [InlineData("DataPageRecordCollectionArchitectureTests.cs")]
    [InlineData("DataPageRecordCommandRouterTests.cs")]
    [InlineData("DataPageRowNavigationControllerTests.cs")]
    [InlineData("DataPageSanierungWindowArchitectureTests.cs")]
    [InlineData("DataPageSelectionChangedControllerTests.cs")]
    [InlineData("DataPageToolbarLayoutTests.cs")]
    [InlineData("DataPageVideoAnalysisArchitectureTests.cs")]
    [InlineData("DataPageVideoPlaybackArchitectureTests.cs")]
    [InlineData("DataPageVideoPathArchitectureTests.cs")]
    [InlineData("DataPageVideoRelinkArchitectureTests.cs")]
    [InlineData("DataGridWrappingTextColumnFactoryTests.cs")]
    [InlineData("DataGridHorizontalAlignmentToTextAlignmentConverterTests.cs")]
    [InlineData("DesignAuditChromeAndGlyphTests.cs")]
    [InlineData("DesignAuditDialogMigrationTests.cs")]
    [InlineData("DesignAuditPlayerCodingSidePanelTests.cs")]
    [InlineData("DesignAuditThemeResourceTests.cs")]
    [InlineData("GridDockingControllerTests.cs")]
    [InlineData("ImportArchitectureGuardTests.cs")]
    [InlineData("PageViewModelLifecycleTests.cs")]
    [InlineData("PlayerWindowCodingStatisticsArchitectureTests.cs")]
    [InlineData("PlayerWindowCodingStateArchitectureTests.cs")]
    [InlineData("PlayerWindowCoreArchitectureTests.cs")]
    [InlineData("PlayerWindowControllerArchitectureTests.cs")]
    [InlineData("PlayerWindowInlineEvidenceArchitectureTests.cs")]
    [InlineData("PlayerWindowLiveDetectionArchitectureTests.cs")]
    [InlineData("PlayerWindowMediaInfrastructureArchitectureTests.cs")]
    [InlineData("PlayerWindowOverlayInputArchitectureTests.cs")]
    [InlineData("PlayerWindowPlaybackArchitectureTests.cs")]
    [InlineData("PlayerWindowPrimaryDamageArchitectureTests.cs")]
    [InlineData("PlayerWindowProtocolPdfArchitectureTests.cs")]
    [InlineData("PlayerWindowProtocolEventMappingArchitectureTests.cs")]
    [InlineData("PlayerWindowResourceDictionaryTests.cs")]
    [InlineData("PlayerWindowRuntimeArchitectureTests.cs")]
    [InlineData("PlayerWindowShellProjectArchitectureTests.cs")]
    [InlineData("PlayerWindowStretchDamageArchitectureTests.cs")]
    [InlineData("PlayerWindowTimerArchitectureTests.cs")]
    [InlineData("PlayerWindowVisualInfrastructureArchitectureTests.cs")]
    [InlineData("PlayerWindowWiringArchitectureTests.cs")]
    [InlineData("ProjektEroeffnungShellGuardTests.cs")]
    [InlineData("SchaechtePageArchitectureGuardTests.cs")]
    [InlineData("SchaechtePageColumnLayoutRefactorTests.cs")]
    [InlineData("ShellNavigationPolicyTests.cs")]
    [InlineData("SystemMonitorProcessSafetyTests.cs")]
    [InlineData("TrainingCenterBatchImportThreadingTests.cs")]
    [InlineData("TrainingCenterSelfTrainingArchitectureTests.cs")]
    [InlineData("TrainingCenterUiThreadArchitectureTests.cs")]
    [InlineData("TrainingCenterPersistenceGuardTests.cs")]
    [InlineData("TrainingCenterReviewCodeExplorerTests.cs")]
    [InlineData("TrainingCenterReviewSamPersistenceTests.cs")]
    [InlineData("TrainingCenterReviewThreadingTests.cs")]
    [InlineData("TrainingFfmpegPathResolverTests.cs")]
    [InlineData("VideoLabelToolCodeBrowserTests.cs")]
    [InlineData("VideoLabelToolSelectionTests.cs")]
    [InlineData("VideoLabelToolServerSecurityTests.cs")]
    [InlineData("VideoLabelToolVisualStyleTests.cs")]
    [InlineData("VsaCodeExplorerCollectionDispatchTests.cs")]
    [InlineData("VsaCodeExplorerWindowDispatcherTests.cs")]
    public void Focused_architecture_tests_use_shared_source_text_helpers(string fileName)
    {
        var source = File.ReadAllText(Path.Combine(
            SourceTextTestHelpers.FindRepositoryRoot(),
            "tests",
            "AuswertungPro.Next.UI.Tests",
            fileName));

        Assert.DoesNotContain("private static string FindRepositoryRoot", source);
        Assert.DoesNotContain("private static string FindRepoRoot", source);
        Assert.DoesNotContain("private static string FindRepoFile", source);
        Assert.DoesNotContain("private static string RepoFile", source);
        Assert.DoesNotContain("internal static string RepoFile", source);
        Assert.DoesNotContain("private static string ExtractMethod(", source);
        Assert.DoesNotContain("private static string ExtractMethodBody", source);
    }
}
