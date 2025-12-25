/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
MIT License

Copyright (c) 2025 Lone DMA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 *
*/

using ImGuiNET;
using LoneEftDmaRadar.Tarkov.GameWorld.Exits;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.Tarkov.GameWorld.Quests;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Skia;
using Silk.NET.OpenGL;
using static SDK.Enums;

namespace LoneEftDmaRadar.UI.Widgets
{
    /// <summary>
    /// Aimview 'widget' that renders Skia content to an FBO-backed texture for ImGui display.
    /// </summary>
    public static class AimviewWidget
    {
        // Constants
        public const float AimviewBaseStrokeSize = 1.33f;

        private static GL _gl;
        private static GRContext _grContext;

        // OpenGL resources
        private static uint _fbo;
        private static uint _texture;
        private static uint _depthRbo;

        // Skia resources
        private static SKSurface _surface;
        private static GRBackendRenderTarget _renderTarget;

        private static int _currentWidth;
        private static int _currentHeight;

        // Flag to track if we need to render this frame
        private static bool _needsRender;
        private static int _pendingWidth;
        private static int _pendingHeight;

        // Aimview camera state
        private static Vector3 _forward, _right, _up, _camPos;

        // Config shortcuts
        private static AimviewWidgetConfig Config => Program.Config.AimviewWidget;

        /// <summary>
        /// Whether the Aimview panel is open.
        /// </summary>
        public static bool IsOpen
        {
            get => Config.Enabled;
            set => Config.Enabled = value;
        }

        // Data sources
        private static LocalPlayer LocalPlayer => Memory.LocalPlayer;
        private static IReadOnlyCollection<AbstractPlayer> AllPlayers => Memory.Players;
        private static IEnumerable<LootItem> FilteredLoot => Memory.Loot?.FilteredLoot;
        private static IEnumerable<StaticLootContainer> Containers => Memory.Loot?.StaticContainers;
        private static IReadOnlyCollection<IExitPoint> Exits => Memory.Exits;
        private static QuestManager QuestManager => Memory.QuestManager;
        private static bool InRaid => Memory.InRaid;

        public static void Initialize(GL gl, GRContext grContext)
        {
            _gl = gl;
            _grContext = grContext;
        }

        /// <summary>
        /// Called from the Skia render phase (before ImGui) to render to the FBO.
        /// </summary>
        public static void RenderToFbo()
        {
            if (!_needsRender || _surface is null || _fbo == 0)
                return;

            _needsRender = false;

            int width = _pendingWidth;
            int height = _pendingHeight;

            // Bind our FBO for Skia rendering
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            _gl.Viewport(0, 0, (uint)width, (uint)height);

            // Draw to Skia surface
            var canvas = _surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            try
            {
                if (InRaid && LocalPlayer is LocalPlayer localPlayer)
                {
                    // Update camera matrix
                    UpdateMatrix(localPlayer);

                    // Draw exfils (always unlimited distance, furthest layer)
                    if (Config.ShowExfils)
                        DrawExfils(canvas, localPlayer, width, height);

                    // Draw quest locations
                    if (Config.ShowQuestLocations && Program.Config.QuestHelper.Enabled)
                        DrawQuestLocations(canvas, localPlayer, width, height);

                    // Draw corpses
                    if (Config.ShowCorpses && Program.Config.Loot.Enabled && !Program.Config.Loot.HideCorpses)
                        DrawCorpses(canvas, localPlayer, width, height);

                    // Draw containers
                    if (Config.ShowContainers && Program.Config.Loot.Enabled && Program.Config.Containers.Enabled)
                        DrawContainers(canvas, localPlayer, width, height);

                    // Draw loot
                    if (Config.ShowLoot && Program.Config.Loot.Enabled)
                        DrawLoot(canvas, localPlayer, width, height);

                    // Draw players
                    DrawPlayers(canvas, localPlayer, width, height);

                    // Draw crosshair
                    DrawCrosshair(canvas, width, height);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteLine($"CRITICAL AIMVIEW PANEL RENDER ERROR: {ex}");
            }

            // Flush Skia to the FBO
            canvas.Flush();
            _grContext.Flush();

            // Unbind FBO - return to default framebuffer
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        /// <summary>
        /// Draw the ImGui window (called during ImGui phase).
        /// </summary>
        public static void Draw()
        {
            if (!IsOpen)
                return;

            // Default size for first use - ImGui persists position/size to imgui.ini automatically
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 300), ImGuiCond.FirstUseEver);

            bool isOpen = IsOpen;
            if (!ImGui.Begin("Aimview", ref isOpen, ImGuiWindowFlags.None))
            {
                IsOpen = isOpen;
                ImGui.End();
                return;
            }
            IsOpen = isOpen;

            // Get the size of the content region
            var avail = ImGui.GetContentRegionAvail();
            int width = Math.Max(64, (int)avail.X);
            int height = Math.Max(64, (int)avail.Y);

            // Recreate FBO/surface if size changed
            EnsureFbo(width, height);

            // Request render for next frame
            _needsRender = true;
            _pendingWidth = width;
            _pendingHeight = height;

            if (_texture != 0)
            {
                // Display texture in ImGui (flip Y because OpenGL textures are bottom-up)
                ImGui.Image((nint)_texture, new System.Numerics.Vector2(width, height),
                    new System.Numerics.Vector2(0, 1), new System.Numerics.Vector2(1, 0));
            }
            else
            {
                ImGui.Text("Surface not available");
            }

            ImGui.End();
        }

        #region Aimview Rendering

        private static void UpdateMatrix(LocalPlayer localPlayer)
        {
            float yaw = localPlayer.Rotation.X * (MathF.PI / 180f);   // horizontal
            float pitch = localPlayer.Rotation.Y * (MathF.PI / 180f); // vertical

            float cy = MathF.Cos(yaw);
            float sy = MathF.Sin(yaw);
            float cp = MathF.Cos(pitch);
            float sp = MathF.Sin(pitch);

            _forward = new Vector3(
                sy * cp,   // X
               -sp,        // Y (up/down tilt)
                cy * cp    // Z
            );
            _forward = Vector3.Normalize(_forward);

            _right = new Vector3(cy, 0f, -sy);
            _right = Vector3.Normalize(_right);

            _up = Vector3.Normalize(Vector3.Cross(_right, _forward));
            _up = -_up;

            _camPos = localPlayer.LookPosition;
        }

        /// <summary>
        /// Bone connections for skeleton rendering.
        /// </summary>
        private static readonly (Bones From, Bones To)[] BoneConnections = new[]
        {
            // Spine
            (Bones.HumanHead, Bones.HumanNeck),
            (Bones.HumanNeck, Bones.HumanSpine3),
            (Bones.HumanSpine3, Bones.HumanSpine2),
            (Bones.HumanSpine2, Bones.HumanSpine1),
            (Bones.HumanSpine1, Bones.HumanPelvis),
            
            // Left Arm
            (Bones.HumanNeck, Bones.HumanLUpperarm),
            (Bones.HumanLUpperarm, Bones.HumanLForearm1),
            (Bones.HumanLForearm1, Bones.HumanLPalm),
            
            // Right Arm
            (Bones.HumanNeck, Bones.HumanRUpperarm),
            (Bones.HumanRUpperarm, Bones.HumanRForearm1),
            (Bones.HumanRForearm1, Bones.HumanRPalm),
            
            // Left Leg
            (Bones.HumanPelvis, Bones.HumanLThigh1),
            (Bones.HumanLThigh1, Bones.HumanLCalf),
            (Bones.HumanLCalf, Bones.HumanLFoot),
            
            // Right Leg
            (Bones.HumanPelvis, Bones.HumanRThigh1),
            (Bones.HumanRThigh1, Bones.HumanRCalf),
            (Bones.HumanRCalf, Bones.HumanRFoot),
        };

        private static void DrawPlayers(SKCanvas canvas, LocalPlayer localPlayer, int width, int height)
        {
            var players = AllPlayers?
                .Where(p => p.IsActive && p.IsAlive && p != localPlayer);

            if (players is null)
                return;

            float scale = Program.Config.UI.UIScale;

            foreach (var player in players)
            {
                // Filter by config
                bool isAI = player.Type == PlayerType.AIScav || player.Type == PlayerType.AIRaider || player.Type == PlayerType.AIBoss;
                bool isEnemy = player.Type == PlayerType.PMC || player.Type == PlayerType.PScav;
                bool isTeammate = player.Type == PlayerType.Teammate;
                
                if (isAI && !Config.ShowAI)
                    continue;
                if (isEnemy && !Config.ShowEnemyPlayers)
                    continue;
                if (isTeammate && !Config.ShowTeammates)
                    continue;

                // Check distance limits
                float distance = Vector3.Distance(localPlayer.LookPosition, player.Position);
                
                if (isAI && Config.AIMaxDistance > 0 && distance > Config.AIMaxDistance)
                    continue;
                if (!isAI && Config.PlayerMaxDistance > 0 && distance > Config.PlayerMaxDistance)
                    continue;
                if (distance > Program.Config.UI.MaxDistance)
                    continue;

                // Get paint color for this player
                var paint = GetPaint(player);

                // Check if skeleton should be shown
                bool showSkeleton = isAI ? Config.ShowSkeletonAI : Config.ShowSkeleton;
                
                if (showSkeleton && player.Skeleton is not null)
                {
                    // Draw skeleton - no blip
                    DrawPlayerSkeleton(canvas, player, width, height, distance, paint);
                    
                    // Draw head circle if enabled (on top of skeleton)
                    bool showHeadCircle = isAI ? Config.ShowHeadCircleAI : Config.ShowHeadCircle;
                    if (showHeadCircle)
                    {
                        DrawPlayerHeadCircle(canvas, player, width, height, distance, paint);
                    }

                    // Draw label near head position
                    DrawPlayerLabel(canvas, player, width, height, distance, isAI, paint);
                }
                else
                {
                    // No skeleton - skip rendering this player entirely
                    // (User chose skeleton mode but skeleton not available, or skeleton disabled)
                    // Only render if we can project the position to screen
                    if (!WorldToScreen(in player.Position, width, height, out var screen))
                        continue;

                    // If skeleton is disabled, we still want to show something minimal
                    // Draw head circle only if explicitly enabled
                    bool showHeadCircle = isAI ? Config.ShowHeadCircleAI : Config.ShowHeadCircle;
                    if (showHeadCircle)
                    {
                        DrawPlayerHeadCircle(canvas, player, width, height, distance, paint);
                    }
                    else
                    {
                        // Draw a small indicator dot if no skeleton and no head circle
                        float minRadius = 2f * scale;
                        canvas.DrawCircle(screen, minRadius, paint);
                    }

                    // Draw label
                    DrawPlayerLabelAtPosition(canvas, screen, player, distance, isAI, paint);
                }
            }
        }

        private static void DrawPlayerLabel(SKCanvas canvas, AbstractPlayer player, int width, int height, float distance, bool isAI, SKPaint paint)
        {
            // Try to get head position for label placement
            Vector3 headPos;
            if (player.Skeleton?.TryGetBonePosition(Bones.HumanHead, out var actualHead) == true && actualHead != Vector3.Zero)
            {
                headPos = actualHead;
            }
            else
            {
                headPos = player.Position;
                headPos.Y += 1.6f;
            }

            if (!WorldToScreen(in headPos, width, height, out var screen))
                return;

            DrawPlayerLabelAtPosition(canvas, screen, player, distance, isAI, paint);
        }

        private static void DrawPlayerLabelAtPosition(SKCanvas canvas, SKPoint screen, AbstractPlayer player, float distance, bool isAI, SKPaint paint)
        {
            var labelParts = new List<string>();
            
            bool showNames = isAI ? Config.ShowAINames : Config.ShowPlayerNames;
            bool showDistance = isAI ? Config.ShowAIDistance : Config.ShowPlayerDistance;
            bool showHealth = isAI ? Config.ShowAIHealth : Config.ShowPlayerHealth;

            if (showNames && !string.IsNullOrEmpty(player.Name))
                labelParts.Add(player.Name);
            if (showHealth && player is ObservedPlayer obs && obs.HealthStatus != ETagStatus.Healthy)
                labelParts.Add($"[{GetHealthStatusText(obs.HealthStatus)}]");
            if (showDistance)
                labelParts.Add($"{distance:n0}m");

            if (labelParts.Count > 0)
            {
                string label = string.Join(" ", labelParts);
                var textPaint = GetTextPaint(player);
                // Position label above the screen position
                canvas.DrawText(label, new SKPoint(screen.X + 5, screen.Y - 5), SKTextAlign.Left, SKFonts.AimviewWidgetFont, textPaint);
            }
        }

        private static void DrawPlayerSkeleton(SKCanvas canvas, AbstractPlayer player, int width, int height, float distance, SKPaint paint)
        {
            if (player.Skeleton?.BoneTransforms is null || player.Skeleton.BoneTransforms.Count == 0)
                return;

            // Calculate line thickness based on distance
            float distanceScale = Math.Clamp(50f / Math.Max(distance, 5f), 0.5f, 2.5f);
            float lineThickness = Math.Max(0.5f, 1.5f * distanceScale * Program.Config.UI.UIScale);

            var skeletonPaint = new SKPaint
            {
                Color = paint.Color,
                StrokeWidth = lineThickness,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            foreach (var (from, to) in BoneConnections)
            {
                if (!player.Skeleton.TryGetBonePosition(from, out var pos1) ||
                    !player.Skeleton.TryGetBonePosition(to, out var pos2))
                    continue;

                if (pos1 == Vector3.Zero || pos2 == Vector3.Zero)
                    continue;

                if (WorldToScreen(in pos1, width, height, out var screen1) &&
                    WorldToScreen(in pos2, width, height, out var screen2))
                {
                    canvas.DrawLine(screen1, screen2, skeletonPaint);
                }
            }
        }

        private static string GetHealthStatusText(Enums.ETagStatus status)
        {
            return status switch
            {
                Enums.ETagStatus.Dying => "DYING",
                Enums.ETagStatus.BadlyInjured => "BAD",
                Enums.ETagStatus.Injured => "INJ",
                _ => ""
            };
        }

        private static void DrawPlayerHeadCircle(SKCanvas canvas, AbstractPlayer player, int width, int height, float distance, SKPaint paint)
        {
            // Try to get actual head position from skeleton, otherwise approximate
            Vector3 headPos;
            if (player.Skeleton?.TryGetBonePosition(Bones.HumanHead, out var actualHead) == true && actualHead != Vector3.Zero)
            {
                headPos = actualHead;
            }
            else
            {
                // Approximate head position (about 1.6m above base position for standing player)
                headPos = player.Position;
                headPos.Y += 1.6f;
            }

            if (!WorldToScreen(in headPos, width, height, out var headScreen))
                return;

            // Calculate approximate head circle size based on distance
            float distanceScale = Math.Clamp(50f / Math.Max(distance, 1f), 0.3f, 1.5f);
            float radius = Math.Clamp(6f * distanceScale * Program.Config.UI.UIScale, 3f, 20f);

            var headPaint = new SKPaint
            {
                Color = paint.Color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f * distanceScale,
                IsAntialias = true
            };
            canvas.DrawCircle(headScreen.X, headScreen.Y, radius, headPaint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static SKPaint GetTextPaint(AbstractPlayer player)
        {
            if (player.IsFocused)
                return SKPaints.TextFocused;
            if (player is LocalPlayer)
                return SKPaints.TextLocalPlayer;

            return player.Type switch
            {
                PlayerType.Teammate => SKPaints.TextTeammate,
                PlayerType.PMC => SKPaints.TextPMC,
                PlayerType.AIScav => SKPaints.TextScav,
                PlayerType.AIRaider => SKPaints.TextRaider,
                PlayerType.AIBoss => SKPaints.TextBoss,
                PlayerType.PScav => SKPaints.TextPScav,
                PlayerType.SpecialPlayer => SKPaints.TextWatchlist,
                PlayerType.Streamer => SKPaints.TextStreamer,
                _ => SKPaints.TextPMC
            };
        }

        private static void DrawLoot(SKCanvas canvas, LocalPlayer localPlayer, int width, int height)
        {
            if (FilteredLoot is not IEnumerable<LootItem> loot)
                return;

            float boxHalf = 4f * Program.Config.UI.UIScale;
            float maxDist = Config.LootRenderDistanceMax ? float.MaxValue : Config.LootRenderDistance;
            if (maxDist <= 0) maxDist = float.MaxValue;

            foreach (var item in loot)
            {
                var itemPos = item.Position;
                var dist = Vector3.Distance(localPlayer.LookPosition, itemPos);
                if (dist > maxDist)
                    continue;

                if (!WorldToScreen(in itemPos, width, height, out var scrPos))
                    continue;

                // Determine paint based on item type
                SKPaint shapePaint, textPaint;
                
                if (Config.ShowQuestItems && item.IsQuestItem)
                {
                    shapePaint = SKPaints.PaintAimviewWidgetQuestItem;
                    textPaint = SKPaints.TextAimviewWidgetQuestItem;
                }
                else if (Config.ShowWishlisted && item.IsWishlisted)
                {
                    shapePaint = SKPaints.PaintAimviewWidgetWishlistItem;
                    textPaint = SKPaints.TextAimviewWidgetWishlistItem;
                }
                else if (item.IsValuableLoot)
                {
                    shapePaint = new SKPaint { Color = SKColors.Turquoise, Style = SKPaintStyle.Fill, IsAntialias = true };
                    textPaint = new SKPaint { Color = SKColors.Turquoise, IsAntialias = true };
                }
                else
                {
                    shapePaint = SKPaints.PaintAimviewWidgetLoot;
                    textPaint = SKPaints.TextAimviewWidgetLoot;
                }

                DrawBoxAndLabel(canvas, scrPos, boxHalf, $"{item.GetUILabel()} ({dist:n1}m)", shapePaint, textPaint);
            }
        }

        private static void DrawContainers(SKCanvas canvas, LocalPlayer localPlayer, int width, int height)
        {
            if (Containers is not IEnumerable<StaticLootContainer> containers)
                return;

            float boxHalf = 4f * Program.Config.UI.UIScale;
            float maxDist = Config.ContainerDistance;
            if (maxDist <= 0) maxDist = float.MaxValue;

            foreach (var container in containers)
            {
                if (!Program.Config.Containers.Selected.ContainsKey(container.ID ?? "NULL"))
                    continue;

                var cPos = container.Position;
                var dist = Vector3.Distance(localPlayer.LookPosition, cPos);
                if (dist > maxDist)
                    continue;

                if (!WorldToScreen(in cPos, width, height, out var scrPos))
                    continue;

                DrawBoxAndLabel(canvas, scrPos, boxHalf, $"{container.Name} ({dist:n1}m)",
                    SKPaints.PaintAimviewWidgetContainer, SKPaints.TextAimviewWidgetContainer);
            }
        }

        private static void DrawExfils(SKCanvas canvas, LocalPlayer localPlayer, int width, int height)
        {
            if (Exits is not IReadOnlyCollection<IExitPoint> exits)
                return;

            float scale = Program.Config.UI.UIScale;

            foreach (var exit in exits)
            {
                if (!WorldToScreen(in exit.Position, width, height, out var scrPos))
                    continue;

                var dist = Vector3.Distance(localPlayer.LookPosition, exit.Position);
                float size = Math.Clamp(8f * scale - MathF.Sqrt(dist) * 0.1f, 3f, 10f);

                bool isTransit = exit is TransitPoint;
                var paint = isTransit ? SKPaints.PaintAimviewWidgetExfilTransit : SKPaints.PaintAimviewWidgetExfil;
                var textPaint = isTransit ? SKPaints.PaintAimviewWidgetExfilTransit : SKPaints.TextAimviewWidgetExfil;

                // Draw diamond shape for exfils
                var path = new SKPath();
                path.MoveTo(scrPos.X, scrPos.Y - size);
                path.LineTo(scrPos.X + size, scrPos.Y);
                path.LineTo(scrPos.X, scrPos.Y + size);
                path.LineTo(scrPos.X - size, scrPos.Y);
                path.Close();
                canvas.DrawPath(path, paint);

                // Draw label - get name from specific type
                string exitName = exit switch
                {
                    Exfil exfil => exfil.Name ?? "Exfil",
                    TransitPoint transit => transit.Description ?? "Transit",
                    _ => "Exit"
                };
                string label = $"{exitName} ({dist:n0}m)";
                canvas.DrawText(label, new SKPoint(scrPos.X + size + 3, scrPos.Y + 4), SKTextAlign.Left, SKFonts.AimviewWidgetFont, textPaint);
            }
        }

        private static void DrawQuestLocations(SKCanvas canvas, LocalPlayer localPlayer, int width, int height)
        {
            if (QuestManager?.LocationConditions is not IReadOnlyDictionary<string, QuestLocation> locations)
                return;

            float scale = Program.Config.UI.UIScale;

            foreach (var location in locations.Values)
            {
                if (!WorldToScreen(in location.Position, width, height, out var scrPos))
                    continue;

                var dist = Vector3.Distance(localPlayer.LookPosition, location.Position);
                float size = Math.Clamp(6f * scale, 3f, 10f);

                // Draw square for quest zones
                var rect = new SKRect(scrPos.X - size, scrPos.Y - size, scrPos.X + size, scrPos.Y + size);
                canvas.DrawRect(rect, SKPaints.PaintAimviewWidgetQuestZone);

                // Draw label using Name and Type
                string label = $"{location.Name} ({dist:n0}m)";
                canvas.DrawText(label, new SKPoint(scrPos.X + size + 3, scrPos.Y + 4), SKTextAlign.Left, SKFonts.AimviewWidgetFont, SKPaints.TextAimviewWidgetQuestZone);
            }
        }

        private static void DrawCorpses(SKCanvas canvas, LocalPlayer localPlayer, int width, int height)
        {
            // Get corpses from FilteredLoot
            var corpses = FilteredLoot?.OfType<LootCorpse>();
            if (corpses is null)
                return;

            float scale = Program.Config.UI.UIScale;
            float maxDist = Config.LootRenderDistance;
            if (maxDist <= 0) maxDist = float.MaxValue;

            foreach (var corpse in corpses)
            {
                var cPos = corpse.Position;
                var dist = Vector3.Distance(localPlayer.LookPosition, cPos);
                if (dist > maxDist)
                    continue;

                if (!WorldToScreen(in cPos, width, height, out var scrPos))
                    continue;

                float size = Math.Clamp(4f * scale, 2f, 8f);

                // Draw X marker for corpses
                var paint = SKPaints.PaintAimviewWidgetCorpse;
                paint.StrokeWidth = 1.5f;
                paint.Style = SKPaintStyle.Stroke;
                canvas.DrawLine(scrPos.X - size, scrPos.Y - size, scrPos.X + size, scrPos.Y + size, paint);
                canvas.DrawLine(scrPos.X + size, scrPos.Y - size, scrPos.X - size, scrPos.Y + size, paint);
                paint.Style = SKPaintStyle.Fill;

                // Draw label
                string label = corpse.Name ?? "Corpse";
                canvas.DrawText($"{label} ({dist:n1}m)", new SKPoint(scrPos.X + size + 3, scrPos.Y + 4), SKTextAlign.Left, SKFonts.AimviewWidgetFont, SKPaints.TextAimviewWidgetCorpse);
            }
        }

        private static void DrawBoxAndLabel(SKCanvas canvas, SKPoint center, float half, string label, SKPaint boxPaint, SKPaint textPaint)
        {
            var rect = new SKRect(center.X - half, center.Y - half, center.X + half, center.Y + half);
            var textPt = new SKPoint(center.X + half + 3, center.Y + 4);

            canvas.DrawRect(rect, boxPaint);
            canvas.DrawText(label, textPt, SKTextAlign.Left, SKFonts.AimviewWidgetFont, textPaint);
        }

        private static void DrawCrosshair(SKCanvas canvas, int width, int height)
        {
            float centerX = width / 2f;
            float centerY = height / 2f;

            canvas.DrawLine(0, centerY, width, centerY, SKPaints.PaintAimviewWidgetCrosshair);
            canvas.DrawLine(centerX, 0, centerX, height, SKPaints.PaintAimviewWidgetCrosshair);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static SKPaint GetPaint(AbstractPlayer player)
        {
            if (player.IsFocused)
                return SKPaints.PaintAimviewWidgetFocused;
            if (player is LocalPlayer)
                return SKPaints.PaintAimviewWidgetLocalPlayer;

            return player.Type switch
            {
                PlayerType.Teammate => SKPaints.PaintAimviewWidgetTeammate,
                PlayerType.PMC => SKPaints.PaintAimviewWidgetPMC,
                PlayerType.AIScav => SKPaints.PaintAimviewWidgetScav,
                PlayerType.AIRaider => SKPaints.PaintAimviewWidgetRaider,
                PlayerType.AIBoss => SKPaints.PaintAimviewWidgetBoss,
                PlayerType.PScav => SKPaints.PaintAimviewWidgetPScav,
                PlayerType.SpecialPlayer => SKPaints.PaintAimviewWidgetWatchlist,
                PlayerType.Streamer => SKPaints.PaintAimviewWidgetStreamer,
                _ => SKPaints.PaintAimviewWidgetPMC
            };
        }

        private static bool WorldToScreen(in Vector3 world, int width, int height, out SKPoint scr)
        {
            scr = default;

            var dir = world - _camPos;

            float dz = Vector3.Dot(dir, _forward);
            if (dz <= 0f)
                return false;

            float dx = Vector3.Dot(dir, _right);
            float dy = Vector3.Dot(dir, _up);

            // Perspective divide
            float nx = dx / dz;
            float ny = dy / dz;

            const float PSEUDO_FOV = 1.0f;
            nx /= PSEUDO_FOV;
            ny /= PSEUDO_FOV;

            scr.X = width * 0.5f + nx * (width * 0.5f);
            scr.Y = height * 0.5f - ny * (height * 0.5f);

            return !(scr.X < 0 || scr.X > width || scr.Y < 0 || scr.Y > height);
        }

        #endregion

        #region FBO Management

        private static void EnsureFbo(int width, int height)
        {
            if (_fbo != 0 && _currentWidth == width && _currentHeight == height)
                return;

            _currentWidth = width;
            _currentHeight = height;

            // Dispose old resources
            DestroyFbo();

            // Create texture
            _texture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _texture);
            unsafe
            {
                _gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    InternalFormat.Rgba8,
                    (uint)width,
                    (uint)height,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    null);
            }
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

            // Create depth renderbuffer (required for some Skia operations)
            _depthRbo = _gl.GenRenderbuffer();
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthRbo);
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)width, (uint)height);

            // Create FBO
            _fbo = _gl.GenFramebuffer();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _texture, 0);
            _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depthRbo);

            // Check FBO status
            var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != GLEnum.FramebufferComplete)
            {
                Logging.WriteLine($"AimviewPanel: FBO incomplete: {status}");
                _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                DestroyFbo();
                return;
            }

            // Unbind FBO
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _gl.BindTexture(TextureTarget.Texture2D, 0);
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

            // Create Skia surface targeting the FBO
            var fbInfo = new GRGlFramebufferInfo(_fbo, (uint)InternalFormat.Rgba8);
            _renderTarget = new GRBackendRenderTarget(width, height, 0, 8, fbInfo);
            _surface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);

            if (_surface is null)
            {
                Logging.WriteLine("AimviewPanel: Failed to create Skia surface");
                DestroyFbo();
            }
        }

        private static void DestroyFbo()
        {
            _surface?.Dispose();
            _surface = null;

            _renderTarget?.Dispose();
            _renderTarget = null;

            if (_fbo != 0)
            {
                _gl.DeleteFramebuffer(_fbo);
                _fbo = 0;
            }

            if (_texture != 0)
            {
                _gl.DeleteTexture(_texture);
                _texture = 0;
            }

            if (_depthRbo != 0)
            {
                _gl.DeleteRenderbuffer(_depthRbo);
                _depthRbo = 0;
            }
        }

        public static void Cleanup()
        {
            DestroyFbo();
        }

        #endregion
    }
}
