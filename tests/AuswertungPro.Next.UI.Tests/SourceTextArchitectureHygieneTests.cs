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
            "PlayerWindow_state_fields_live_in_state_partial",
            "PlayerWindow_bounds_adjustment_lives_in_policy",
            "PlayerWindow_trace_output_lives_in_player_trace",
            "PlayerWindow_timestamp_access_lives_in_player_clock"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_snapshot_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowSnapshotArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "PlayerWindow-Snapshot-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        var methodNames = new[]
        {
            "PlayerWindow_live_snapshot_temp_path_lives_in_policy",
            "PlayerWindow_public_snapshot_path_lives_in_policy"
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
            "PlayerWindow_uses_status_color_constants",
            "PlayerWindow_coding_visual_tree_helper_lives_in_visual_tree_partial"
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
            "PlayerWindow_playback_lifecycle_lives_in_lifecycle_partial",
            "PlayerWindow_playback_preview_lives_in_policy_and_speed_controls_in_controller",
            "PlayerWindow_playback_controls_live_in_controls_partial",
            "PlayerWindow_playback_timeline_reads_through_timeline_host",
            "PlayerWindow_keyboard_slider_and_button_playback_uses_control_host",
            "PlayerWindow_playback_rate_uses_control_host",
            "PlayerWindow_playback_start_uses_control_host",
            "Playback_position_fallback_uses_timeline_host",
            "PlayerWindow_snapshot_pause_uses_playback_control_host",
            "PlayerWindow_playback_snapshot_lives_in_snapshot_partial",
            "PlayerWindow_marquee_overlay_settings_live_in_policy"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_keyboard_action_guard_lives_in_keyboard_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowKeyboardArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Keyboard-Architektur-Guards sollen in einer eigenen fokussierten Suite liegen.");

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_keyboard_action_execution_lives_in_controller";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_toggle_button_guard_lives_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowToggleButtonArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "ToggleButton-Architekturguard soll in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_toggle_button_state_uses_controls";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
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
            "PlayerWindow_timer_shutdown_uses_stopper",
            "PlayerWindow_osd_timer_gate_uses_policy"
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

        var methodNames = new[]
        {
            "PlayerWindow_open_stretch_damage_prompt_lives_in_policy",
            "PlayerWindow_stretch_damage_close_marker_lives_in_factory",
            "PlayerWindow_stretch_damage_close_decision_lives_in_policy"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
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
            "PlayerWindow_live_detection_model_selection_lives_in_policy",
            "PlayerWindow_live_detection_confirmation_threshold_lives_in_policy",
            "PlayerWindow_live_detection_timer_gate_lives_in_policy",
            "PlayerWindow_live_detection_status_lives_in_status_partial",
            "PlayerWindow_live_detection_lifecycle_lives_in_lifecycle_partial",
            "PlayerWindow_live_detection_dialogs_live_in_service",
            "PlayerWindow_live_detection_snapshot_lives_in_snapshot_partial",
            "PlayerWindow_live_detection_overlay_lives_in_overlay_partial"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_live_detection_confirmation_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowLiveDetectionConfirmationArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "LiveDetection-Confirmation-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_live_detection_confirmation_actions_live_in_actions_partial";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_coding_ai_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingAiArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Coding-AI-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        var methodNames = new[]
        {
            "PlayerWindow_coding_live_ai_wiring_lives_in_live_partial",
            "PlayerWindow_coding_health_monitoring_lives_in_monitoring_partial",
            "PlayerWindow_coding_ai_shared_helpers_live_in_helpers_partial",
            "PlayerWindow_coding_osd_reading_lives_in_reading_partial",
            "PlayerWindow_osd_badge_meter_text_uses_display_policy"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_coding_catalog_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focused = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingCatalogArchitectureTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        const string methodName = "PlayerWindow_code_catalog_helpers_live_in_coding_catalog_partial";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_coding_classifier_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingClassifierArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Coding-Classifier-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_coding_classifier_results_live_in_classifier_partial";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_coding_multi_model_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingMultiModelArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Coding-Multi-Model-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        var methodNames = new[]
        {
            "PlayerWindow_multi_model_analysis_sequence_lives_in_command_workflow",
            "PlayerWindow_multi_model_ai_events_live_in_multimodel_partial"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_coding_ai_events_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingAiEventsArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Coding-AI-Events-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        var methodNames = new[]
        {
            "PlayerWindow_live_ai_events_live_in_live_partial",
            "PlayerWindow_coding_ai_finding_filtering_lives_in_filtering_partial"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_coding_boundary_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingBoundaryArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Coding-Boundary-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        var methodNames = new[]
        {
            "PlayerWindow_boundary_presence_lives_in_policy",
            "PlayerWindow_boundary_import_reference_lives_in_policy"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_live_detection_marking_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowLiveDetectionMarkingArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "LiveDetection-Marking-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        var methodNames = new[]
        {
            "PlayerWindow_manual_mark_bbox_mapping_lives_in_mapper",
            "PlayerWindow_mark_box_quantification_mapping_lives_in_policy",
            "PlayerWindow_mark_segmentation_lives_in_segmentation_partial"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_inline_defect_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowInlineDefectArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Inline-Defekt-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        var methodNames = new[]
        {
            "PlayerWindow_inline_defect_detail_uses_display_policy_state",
            "PlayerWindow_inline_defect_preview_lives_in_preview_partial",
            "PlayerWindow_event_list_right_click_selection_uses_helper",
            "PlayerWindow_coding_event_list_item_coloring_lives_in_list_items_partial",
            "PlayerWindow_coding_side_panel_width_lives_in_policy",
            "PlayerWindow_inline_defect_actions_live_in_actions_partial"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_coding_photo_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingPhotoArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Coding-Foto-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        var methodNames = new[]
        {
            "PlayerWindow_coding_snapshot_target_lives_in_policy",
            "PlayerWindow_coding_photo_capture_lives_in_capture_partial",
            "PlayerWindow_frame_extraction_lives_in_service",
            "PlayerWindow_photo_display_paths_live_in_policy",
            "PlayerWindow_photo_viewer_lives_in_viewer_partial",
            "PlayerWindow_manual_photo_slot_logic_lives_in_policy",
            "PlayerWindow_analyzed_frame_timestamp_lives_in_policy"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_coding_training_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingTrainingArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Coding-Training-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_training_sample_persistence_lives_in_coordinator";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_coding_import_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingImportArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Coding-Import-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_import_reference_transfer_lives_in_policy";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_coding_events_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingEventsArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Coding-Events-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        var methodNames = new[]
        {
            "PlayerWindow_coding_event_display_order_lives_in_policy",
            "PlayerWindow_coding_event_list_surface_uses_controls",
            "PlayerWindow_manual_code_meter_resolution_uses_policy",
            "PlayerWindow_manual_coding_ai_context_lives_in_factory",
            "PlayerWindow_coding_select_code_handler_uses_fire_and_forget_wrapper"
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_coding_protocol_match_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingProtocolMatchArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Coding-Protocol-Match-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        var methodNames = new[]
        {
            "PlayerWindow_import_confirmation_badge_uses_display_policy",
            "PlayerWindow_green_match_accept_overlay_uses_display_policy",
            "PlayerWindow_protocol_match_summary_uses_controls_adapter",
            "PlayerWindow_protocol_match_training_lives_in_training_partial",
            "PlayerWindow_protocol_match_highlighting_lives_in_highlighting_partial",
        };

        foreach (var methodName in methodNames)
        {
            Assert.Contains($"public void {methodName}", focused);
            Assert.DoesNotContain($"public void {methodName}", guard);
        }
    }

    [Fact]
    public void Player_window_coding_apply_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingApplyArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Coding-Apply-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_protocol_revision_update_lives_in_policy";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_coding_event_actions_guards_live_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingEventActionsArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Coding-Event-Aktionsguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_coding_event_actions_live_in_actions_partial";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_explorer_entry_edit_guard_lives_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowExplorerEntryEditArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Explorer-Entry-Edit-Architekturguard soll in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_explorer_entry_edits_use_copier";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_vsa_code_explorer_guard_lives_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowVsaCodeExplorerArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "VSA-Code-Explorer-Architekturguard soll in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_vsa_code_explorer_window_creation_lives_in_dialog_service";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_live_ai_status_guard_lives_in_coding_ai_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingAiArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_live_ai_status_text_uses_display_policy";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_confirmation_action_guard_lives_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingConfirmationArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        Assert.True(File.Exists(focusedPath), "Coding-Confirmation-Architekturguards sollen in einer eigenen fokussierten Testdatei liegen.");

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_confirmation_actions_use_workflows_and_delete_applier";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_confirmation_panel_guard_lives_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingConfirmationArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_confirmation_panel_display_uses_controls_adapter";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_confirmation_playback_guard_lives_in_focused_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingConfirmationArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_confirmation_playback_uses_player_helper";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_coding_interaction_playback_guard_lives_in_playback_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowPlaybackArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_coding_interaction_playback_uses_player_helper";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_live_detection_stop_playback_guard_lives_in_live_detection_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowLiveDetectionArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_live_detection_stop_playback_uses_player_helper";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_live_ai_timer_gate_guard_lives_in_coding_ai_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingAiArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_live_ai_timer_gate_uses_policy";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_live_ai_timer_interval_guard_lives_in_coding_ai_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingAiArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_live_ai_timer_intervals_live_in_settings";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
    }

    [Fact]
    public void Player_window_live_ai_timer_wiring_guard_lives_in_coding_ai_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var focusedPath = Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "PlayerWindowCodingAiArchitectureTests.cs");
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        var focused = File.ReadAllText(focusedPath);
        const string methodName = "PlayerWindow_live_ai_timer_wiring_lives_in_controller";

        Assert.Contains($"public void {methodName}", focused);
        Assert.DoesNotContain($"public void {methodName}", guard);
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
    [InlineData("PlayerWindowCodingBoundaryArchitectureTests.cs")]
    [InlineData("PlayerWindowCodingAiArchitectureTests.cs")]
    [InlineData("PlayerWindowCodingApplyArchitectureTests.cs")]
    [InlineData("PlayerWindowCodingClassifierArchitectureTests.cs")]
    [InlineData("PlayerWindowCodingConfirmationArchitectureTests.cs")]
    [InlineData("PlayerWindowCodingAiEventsArchitectureTests.cs")]
    [InlineData("PlayerWindowCodingEventActionsArchitectureTests.cs")]
    [InlineData("PlayerWindowCodingMultiModelArchitectureTests.cs")]
    [InlineData("PlayerWindowCodingStatisticsArchitectureTests.cs")]
    [InlineData("PlayerWindowCodingStateArchitectureTests.cs")]
    [InlineData("PlayerWindowCodingCatalogArchitectureTests.cs")]
    [InlineData("PlayerWindowCodingImportArchitectureTests.cs")]
    [InlineData("PlayerWindowCodingPhotoArchitectureTests.cs")]
    [InlineData("PlayerWindowCodingTrainingArchitectureTests.cs")]
    [InlineData("PlayerWindowCoreArchitectureTests.cs")]
    [InlineData("PlayerWindowControllerArchitectureTests.cs")]
    [InlineData("PlayerWindowExplorerEntryEditArchitectureTests.cs")]
    [InlineData("PlayerWindowInlineEvidenceArchitectureTests.cs")]
    [InlineData("PlayerWindowInlineDefectArchitectureTests.cs")]
    [InlineData("PlayerWindowLiveDetectionArchitectureTests.cs")]
    [InlineData("PlayerWindowLiveDetectionConfirmationArchitectureTests.cs")]
    [InlineData("PlayerWindowLiveDetectionMarkingArchitectureTests.cs")]
    [InlineData("PlayerWindowMediaInfrastructureArchitectureTests.cs")]
    [InlineData("PlayerWindowOverlayInputArchitectureTests.cs")]
    [InlineData("PlayerWindowPlaybackArchitectureTests.cs")]
    [InlineData("PlayerWindowPrimaryDamageArchitectureTests.cs")]
    [InlineData("PlayerWindowProtocolPdfArchitectureTests.cs")]
    [InlineData("PlayerWindowProtocolEventMappingArchitectureTests.cs")]
    [InlineData("PlayerWindowResourceDictionaryTests.cs")]
    [InlineData("PlayerWindowRuntimeArchitectureTests.cs")]
    [InlineData("PlayerWindowSnapshotArchitectureTests.cs")]
    [InlineData("PlayerWindowShellProjectArchitectureTests.cs")]
    [InlineData("PlayerWindowStretchDamageArchitectureTests.cs")]
    [InlineData("PlayerWindowTimerArchitectureTests.cs")]
    [InlineData("PlayerWindowVisualInfrastructureArchitectureTests.cs")]
    [InlineData("PlayerWindowVsaCodeExplorerArchitectureTests.cs")]
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
