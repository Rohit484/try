using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using FluentAssertions;
using System.Threading.Tasks;
using MLS.Agent.CommandLine;
using MLS.Agent.Tests.TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace MLS.Agent.Tests
{
    public class CommandLineParserTests
    {
        private readonly ITestOutputHelper _output;
        private readonly TestConsole _console = new TestConsole();
        private StartupOptions _start_options;
        private readonly Parser _parser;
        private TryGitHubOptions _tryGitHubOptions;
        private PackOptions _packOptions;
        private InstallOptions _installOptions;
        private DirectoryInfo _install_packageSource;
        private VerifyOptions _verifyOptions;
        private DemoOptions _demoOptions;
        private KernelOptions _kernelOptions;

        public CommandLineParserTests(ITestOutputHelper output)
        {
            _output = output;
            _parser = CommandLineParser.Create(
                start: (options, invocationContext) =>
                {
                    _start_options = options;
                },
                demo: (options, console, context, startOptions) =>
                {
                    _demoOptions = options;
                    return Task.CompletedTask;
                },
                tryGithub: (options, c) =>
                {
                    _tryGitHubOptions = options;
                    return Task.CompletedTask;
                },
                pack: (options, console) =>
                {
                    _packOptions = options;
                    return Task.CompletedTask;
                },
                install: (options, console) =>
                {
                    _installOptions = options;
                    _install_packageSource = options.AddSource;
                    return Task.CompletedTask;
                },
                verify: (options, console) =>
                {
                    _verifyOptions = options;
                    return Task.FromResult(1);
                },
                kernel: (options, console) =>
                {
                    _kernelOptions = options;
                    return Task.FromResult(1);
                }) ;
        }

        [Fact]
        public async Task Parse_empty_command_line_has_sane_defaults()
        {
            await _parser.InvokeAsync("hosted", _console);

            _start_options.Production.Should().BeFalse();
        }

        [Fact]
        public async Task Parse_production_mode_flag_switches_option_to_production()
        {
            await _parser.InvokeAsync("hosted --production", _console);

            _start_options.Production.Should().BeTrue();
        }

        [Fact]
        public async Task Parse_root_directory_with_a_valid_path_succeeds()
        {
            var path = TestAssets.SampleConsole.FullName;
            await _parser.InvokeAsync(new[] { path }, _console);
            _start_options.RootDirectory.FullName.Should().Be(path);
        }

        [Fact]
        public async Task Parse_empty_command_line_has_current_directory_as_root_directory()
        {
            await _parser.InvokeAsync("", _console);
            _start_options.RootDirectory.FullName.Should().Be(Directory.GetCurrentDirectory());
        }

        [Fact]
        public async Task Parse_root_directory_with_a_non_existing_path_fails()
        {
            await _parser.InvokeAsync("INVALIDPATH", _console);
            _start_options.Should().BeNull();
            _console.Error.ToString().Should().Match("*Directory does not exist: INVALIDPATH*");
        }

        [Fact]
        public async Task Parse_uri_workspace()
        {
            await _parser.InvokeAsync("--uri https://google.com/foo.md", _console);
            _start_options.Uri.Should().Be("https://google.com/foo.md");
        }

        [Fact]
        public async Task Parse_enable_preview_features_flag()
        {
            await _parser.InvokeAsync("--enable-preview-features", _console);
            _start_options.EnablePreviewFeatures.Should().BeTrue();
        }


        [Fact]
        public async Task Parse_language_service_mode_flag_switches_option_to_language_service()
        {
            await _parser.InvokeAsync("hosted --language-service", _console);
            _start_options.IsLanguageService.Should().BeTrue();
        }

        [Fact]
        public void Parse_key_without_parameter_fails_the_parse()
        {
            _parser.Parse("hosted -k")
                   .Errors
                   .Should()
                   .Contain(e => e.Message == "Required argument missing for option: -k");

            _parser.Parse("hosted --key")
                   .Errors
                   .Should()
                   .Contain(e => e.Message == "Required argument missing for option: --key");
        }

        [Fact]
        public async Task Parse_key_with_parameter_succeeds()
        {
            await _parser.InvokeAsync("hosted -k abc123", _console);
            _start_options.Key.Should().Be("abc123");

            await _parser.InvokeAsync("hosted --key abc123", _console);
            _start_options.Key.Should().Be("abc123");
        }

        [Fact]
        public async Task AiKey_defaults_to_null()
        {
            await _parser.InvokeAsync("hosted", _console);
            _start_options.ApplicationInsightsKey.Should().BeNull();
        }

        [Fact]
        public void Parse_application_insights_key_without_parameter_fails_the_parse()
        {
            var result = _parser.Parse("hosted --ai-key");

            result.Errors.Should().Contain(e => e.Message == "Required argument missing for option: --ai-key");
        }

        [Fact]
        public async Task Parse_aiKey_with_parameter_succeeds()
        {
            await _parser.InvokeAsync("hosted --ai-key abc123", _console);
            _start_options.ApplicationInsightsKey.Should().Be("abc123");
        }

        [Fact]
        public async Task When_root_command_is_specified_then_agent_is_not_in_hosted_mode()
        {
            await _parser.InvokeAsync("", _console);
            _start_options.IsInHostedMode.Should().BeFalse();
        }

        [Fact]
        public async Task When_hosted_command_is_specified_then_agent_is_in_hosted_mode()
        {
            await _parser.InvokeAsync("hosted", _console);
            _start_options.IsInHostedMode.Should().BeTrue();
        }

        [Fact]
        public async Task GitHub_handler_not_run_if_argument_is_missing()
        {
            await _parser.InvokeAsync("github");
            _tryGitHubOptions.Should().BeNull();
        }

        [Fact]
        public async Task GitHub_handler_run_if_argument_is_present()
        {
            await _parser.InvokeAsync("github roslyn");
            _tryGitHubOptions.Repo.Should().Be("roslyn");
        }

        [Fact]
        public async Task Pack_not_run_if_argument_is_missing()
        {
            var console = new TestConsole();
            await _parser.InvokeAsync("pack", console);
            console.Out.ToString().Should().Contain("pack <PackTarget>");
            _packOptions.Should().BeNull();
        }

        [Fact]
        public async Task Pack_parses_directory_info()
        {
            var console = new TestConsole();
            var expected = Path.GetDirectoryName(typeof(PackCommand).Assembly.Location);

            await _parser.InvokeAsync($"pack {expected}", console);
            _packOptions.PackTarget.FullName.Should().Be(expected);
        }

        [Fact]
        public async Task Install_not_run_if_argument_is_missing()
        {
            var console = new TestConsole();
            await _parser.InvokeAsync("install", console);
            console.Out.ToString().Should().Contain("install [options] <PackageName>");
            _installOptions.Should().BeNull();
        }

        [Fact]
        public async Task Install_parses_source_option()
        {
            var console = new TestConsole();

            var expectedPackageSource = Path.GetDirectoryName(typeof(PackCommand).Assembly.Location);

            await _parser.InvokeAsync($"install --add-source {expectedPackageSource} the-package", console);

            _installOptions.PackageName.Should().Be("the-package");
            _install_packageSource.FullName.Should().Be(expectedPackageSource);
        }

        [Fact]
        public async Task Verify_argument_specifies_root_directory()
        {
            var directory = Path.GetDirectoryName(typeof(VerifyCommand).Assembly.Location);
             await _parser.InvokeAsync($"verify {directory}");
            _verifyOptions.RootDirectory.FullName.Should().Be(directory);
        }

        [Fact]
        public async Task Demo_allows_output_path_to_be_specified()
        {
            var expected = Path.GetTempPath();

            await _parser.InvokeAsync($"demo --output {expected}");

            _demoOptions
                .Output
                .FullName
                .Should()
                .Be(expected);
        }

        [Fact]
        public async Task Demo_allows_enabling_preview_features()
        {
            var expected = Path.GetTempPath();

            await _parser.InvokeAsync($"demo --output {expected} --enable-preview-features");

            _demoOptions
                .Output
                .FullName
                .Should()
                .Be(expected);

            _demoOptions
                .EnablePreviewFeatures
                .Should()
                .BeTrue();
        }
    }
}