using System.Linq;
using NUnit.Framework;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using UnityEditor;

namespace MCPForUnity.Editor.Tests.EditMode.Services
{
    [TestFixture]
    public class ToolDiscoveryServiceTests
    {
        private const string TestToolName = "test_tool_for_testing";

        [SetUp]
        public void SetUp()
        {
            // Clean up any test preferences
            DeleteToolPreferenceKeys(TestToolName);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test preferences after each test
            DeleteToolPreferenceKeys(TestToolName);
        }

        [Test]
        public void SetToolEnabled_WritesToEditorPrefs()
        {
            // Arrange
            var service = new ToolDiscoveryService();

            // Act
            service.SetToolEnabled(TestToolName, false);

            // Assert
            string key = GetProjectScopedToolPreferenceKey(TestToolName);
            Assert.IsTrue(EditorPrefs.HasKey(key), "Preference key should exist after SetToolEnabled");
            Assert.IsFalse(EditorPrefs.GetBool(key, true), "Preference should be set to false");
        }

        [Test]
        public void IsToolEnabled_ReturnsFalse_WhenToolDoesNotExist()
        {
            // Arrange - Ensure no preference exists
            DeleteToolPreferenceKeys(TestToolName);

            var service = new ToolDiscoveryService();

            // Act - For a non-existent tool, IsToolEnabled should return false
            // (since metadata.AutoRegister defaults to false for non-existent tools)
            bool result = service.IsToolEnabled(TestToolName);

            // Assert - Non-existent tools return false (no metadata found)
            Assert.IsFalse(result, "Non-existent tool should return false");
        }

        [Test]
        public void IsToolEnabled_ReturnsStoredValue_WhenPreferenceExists()
        {
            // Arrange
            string key = GetProjectScopedToolPreferenceKey(TestToolName);
            EditorPrefs.SetBool(key, false);  // Store false value
            var service = new ToolDiscoveryService();

            // Act
            bool result = service.IsToolEnabled(TestToolName);

            // Assert
            Assert.IsFalse(result, "Should return the stored preference value (false)");
        }

        [Test]
        public void IsToolEnabled_ReturnsTrue_WhenPreferenceSetToTrue()
        {
            // Arrange
            string key = GetProjectScopedToolPreferenceKey(TestToolName);
            EditorPrefs.SetBool(key, true);
            var service = new ToolDiscoveryService();

            // Act
            bool result = service.IsToolEnabled(TestToolName);

            // Assert
            Assert.IsTrue(result, "Should return the stored preference value (true)");
        }

        [Test]
        public void IsToolEnabled_MigratesLegacyGlobalPreference_WhenProjectPreferenceMissing()
        {
            // Arrange
            string projectKey = GetProjectScopedToolPreferenceKey(TestToolName);
            string legacyKey = GetLegacyToolPreferenceKey(TestToolName);
            EditorPrefs.DeleteKey(projectKey);
            EditorPrefs.SetBool(legacyKey, false);

            var service = new ToolDiscoveryService();

            // Act
            bool result = service.IsToolEnabled(TestToolName);

            // Assert
            Assert.IsFalse(result, "Should return the legacy preference value when project preference is missing");
            Assert.IsTrue(EditorPrefs.HasKey(projectKey), "Legacy preference should be copied to the project-scoped key");
            Assert.IsFalse(EditorPrefs.GetBool(projectKey, true), "Project-scoped preference should preserve the legacy false value");
        }

        [Test]
        public void ToolToggle_PersistsAcrossServiceInstances()
        {
            // Arrange
            var service1 = new ToolDiscoveryService();
            service1.SetToolEnabled(TestToolName, false);

            // Act - Create a new service instance
            var service2 = new ToolDiscoveryService();
            bool result = service2.IsToolEnabled(TestToolName);

            // Assert - The disabled state should persist
            Assert.IsFalse(result, "Tool state should persist across service instances");
        }

        [Test]
        public void DiscoverAllTools_DoesNotOverrideStoredFalse_ForBuiltInAutoRegisterFalseTool()
        {
            // Arrange
            var service = new ToolDiscoveryService();
            var builtInTool = service.DiscoverAllTools()
                .FirstOrDefault(tool => tool.IsBuiltIn && !tool.AutoRegister);

            Assert.IsNotNull(builtInTool, "Expected at least one built-in tool with AutoRegister=false.");

            string key = GetProjectScopedToolPreferenceKey(builtInTool.Name);
            bool hadOriginalKey = EditorPrefs.HasKey(key);
            bool originalValue = hadOriginalKey && EditorPrefs.GetBool(key, true);

            try
            {
                EditorPrefs.SetBool(key, false);
                service.InvalidateCache();

                // Act
                service.DiscoverAllTools();
                bool enabled = service.IsToolEnabled(builtInTool.Name);

                // Assert
                Assert.IsFalse(enabled, $"Built-in tool '{builtInTool.Name}' should remain disabled when preference is false.");
            }
            finally
            {
                if (hadOriginalKey)
                {
                    EditorPrefs.SetBool(key, originalValue);
                }
                else
                {
                    EditorPrefs.DeleteKey(key);
                }
            }
        }

        private static string GetProjectScopedToolPreferenceKey(string toolName)
        {
            return $"{EditorPrefKeys.ToolEnabledPrefix}{ProjectIdentityUtility.GetProjectHash()}.{toolName}";
        }

        private static string GetLegacyToolPreferenceKey(string toolName)
        {
            return EditorPrefKeys.ToolEnabledPrefix + toolName;
        }

        private static void DeleteToolPreferenceKeys(string toolName)
        {
            EditorPrefs.DeleteKey(GetProjectScopedToolPreferenceKey(toolName));
            EditorPrefs.DeleteKey(GetLegacyToolPreferenceKey(toolName));
        }
    }
}
