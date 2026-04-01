
```csharp
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AiTeam.Pages
{
    public partial class AgentSettings : ComponentBase
    {
        // Agent model
        public class Agent
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public bool IsEnabled { get; set; }
        }

        // Notification model for displaying messages
        public class Notification
        {
            public string Message { get; set; } = string.Empty;
            public string Type { get; set; } = "info"; // info, success, warning, error
            public bool IsVisible { get; set; }
            public int? AgentId { get; set; } // Track which agent triggered the notification
        }

        // Current notification state
        public Notification CurrentNotification { get; set; } = new Notification();

        // List of agents
        public List<Agent> Agents { get; set; } = new List<Agent>();

        // Loading state
        public bool IsLoading { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await LoadAgentsAsync();
        }

        public async Task LoadAgentsAsync()
        {
            IsLoading = true;
            
            // Simulate loading agents
            await Task.Delay(10); // Minimal delay for async operation
            
            Agents = new List<Agent>
            {
                new Agent { Id = 1, Name = "Customer Service Agent", Description = "Handles customer inquiries", IsEnabled = true },
                new Agent { Id = 2, Name = "Sales Agent", Description = "Manages sales processes", IsEnabled = false },
                new Agent { Id = 3, Name = "Technical Support Agent", Description = "Provides technical assistance", IsEnabled = true }
            };
            
            IsLoading = false;
        }

        public async Task ToggleAgentStatusAsync(Agent agent)
        {
            if (agent == null)
                return;

            var previousStatus = agent.IsEnabled;
            
            try
            {
                // Toggle the status
                agent.IsEnabled = !agent.IsEnabled;
                
                // Simulate API call
                await Task.Delay(10);
                
                // Show success notification near the toggle button
                ShowNotification(
                    agent.IsEnabled 
                        ? $"Agent '{agent.Name}' has been enabled successfully." 
                        : $"Agent '{agent.Name}' has been disabled successfully.",
                    "success",
                    agent.Id
                );
            }
            catch (Exception ex)
            {
                // Revert on error
                agent.IsEnabled = previousStatus;
                
                ShowNotification(
                    $"Failed to update agent status: {ex.Message}",
                    "error",
                    agent.Id
                );
            }
        }

        public void ShowNotification(string message, string type = "info", int? agentId = null)
        {
            CurrentNotification = new Notification
            {
                Message = message,
                Type = type,
                IsVisible = true,
                AgentId = agentId
            };

            // Auto-hide notification after delay
            _ = AutoHideNotificationAsync();
        }

        public async Task AutoHideNotificationAsync()
        {
            await Task.Delay(3000);
            HideNotification();
            StateHasChanged();
        }

        public void HideNotification()
        {
            CurrentNotification.IsVisible = false;
        }

        public string GetNotificationCssClass()
        {
            return CurrentNotification.Type switch
            {
                "success" => "notification notification-success",
                "error" => "notification notification-error",
                "warning" => "notification notification-warning",
                _ => "notification notification-info"
            };
        }

        public bool IsNotificationVisibleForAgent(int agentId)
        {
            return CurrentNotification.IsVisible && 
                   CurrentNotification.AgentId.HasValue && 
                   CurrentNotification.AgentId.Value == agentId;
        }

        public bool IsGlobalNotificationVisible()
        {
            return CurrentNotification.IsVisible && 
                   !CurrentNotification.AgentId.HasValue;
        }
    }
}
```