﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nucleus.Coop.Forms;
using Nucleus.Coop.Tools;
using Nucleus.Gaming;
using Nucleus.Gaming.Cache;
using Nucleus.Gaming.Controls;
using Nucleus.Gaming.Coop;
using Nucleus.Gaming.Coop.Generic;
using Nucleus.Gaming.Coop.InputManagement;
using Nucleus.Gaming.Coop.ProtoInput;
using Nucleus.Gaming.Generic.Step;
using Nucleus.Gaming.Platform.PCSpecs;
using Nucleus.Gaming.Tools.GlobalWindowMethods;
using Nucleus.Gaming.Util;
using Nucleus.Gaming.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Threading;
using System.Windows.Forms;


namespace Nucleus.Coop
{
    /// <summary>
    /// Central UI class to the Nucleus Coop application
    /// </summary>
    public partial class MainForm : BaseForm, IDynamicSized
    {
        public readonly string version = "v" + Globals.Version;
        public readonly IniFile iconsIni;
        public readonly IniFile themeIni = Globals.ThemeIni;
        public readonly string theme = Globals.Theme;

        protected string faq_link = "https://www.splitscreen.me/docs/faq";
        protected string api = "https://hub.splitscreen.me/api/v1/";
        private string NucleusEnvironmentRoot = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private string DocumentsRoot = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public string customFont;
        private string currentGameSetup;

        public string[] rgb_font;
        public string[] rgb_MouseOverColor;
        public string[] rgb_MenuStripBackColor;
        public string[] rgb_MenuStripFontColor;
        public string[] rgb_TitleBarColor;
        public string[] rgb_HandlerNoteTitleFont;
        public string[] rgb_HandlerNoteFontColor;
        public string[] rgb_HandlerNoteTitleFontColor;
        public string[] rgb_ButtonsBorderColor;
        public string[] rgb_ThirdPartyToolsLinks;
        public string[] rgb_HandlerNoteBackColor;
        public string[] rgb_HandlerNoteMagnifierTitleBackColor;

        public Form splashscreen = new Splashscreen();
        public XInputShortcutsSetup Xinput_S_Setup;
        private NewSettings settings = null;
        private ProfileSettings profileSettings = null;
        private SearchDisksForm searchDisksForm = null;

        private ContentManager content;
        private IGameHandler I_GameHandler;
        private GameManager gameManager;
        private Dictionary<UserGameInfo, GameControl> controls;
       
        private GameControl currentControl;
        private UserGameInfo currentGameInfo;
        private GenericGameInfo currentGame;
        public HubShowcase hubShowcase;
        private GameProfile currentProfile;
        
        private UserInputControl currentStep;
        public PositionsControl positionsControl;
        private PlayerOptionsControl optionsControl;
        private JSUserInputControl jsControl;
        private Handler handler = null;
        private ScriptDownloader scriptDownloader;
        private DownloadPrompt downloadPrompt;
        private SoundPlayer splayer;
        private AssetsDownloader assetsDownloader;

        private int KillProcess_HotkeyID = 1;
        private int TopMost_HotkeyID = 2;
        private int StopSession_HotkeyID = 3;
        private int SetFocus_HotkeyID = 4;
        private int ResetWindows_HotkeyID = 5;
        private int Cutscenes_HotkeyID = 6;
        private int Switch_HotkeyID = 7;

        private int currentStepIndex;

        private List<string> profilePaths = new List<string>();
        private List<Control> ctrls = new List<Control>();
        private List<UserInputControl> stepsList;

        private Thread handlerThread;
        public Action<IntPtr> RawInputAction { get; set; }

        public Bitmap defBackground;
        public Bitmap coverImg;
        public Bitmap screenshotImg;
        public Color buttonsBackColor;
        private Bitmap favorite_Unselected;
        private Bitmap favorite_Selected;

        public bool restartRequired = false;

        private bool Splash_On;
        private bool ToggleCutscenes = false;
        private bool formClosing;
        private bool noGamesPresent;
        public bool mouseClick;
        public bool roundedcorners;
        public bool useButtonsBorder;
        private bool DisableOfflineIcon;
        private bool showFavoriteOnly;
        private bool canResize = false;
        private bool disableFastHandlerUpdate = false;
        private bool hotkeysLocked = false;

        private bool disableGameProfiles;
        public bool DisableGameProfiles
        {
            get => disableGameProfiles;
            set
            {
                if (disableGameProfiles != value)
                {
                    disableGameProfiles = value;
                    RefreshUI();
                }
            }
        }

        private static bool connected;
        public bool Connected
        {
            get => connected;
            set
            {
                connected = value;
                Hub.Connected = value;

                if (value == true)
                {
                    RefreshGames(value);
                    btn_noHub.Visible = false;
                    btn_downloadAssets.Enabled = true;
                    btn_Download.Enabled = true;

                    if (currentControl != null)
                    {
                        button_UpdateAvailable.Visible = currentControl.GameInfo.UpdateAvailable;
                    }                   
                }
            }
        }

        private System.Windows.Forms.Timer DisposeTimer;//dispose splash screen timer
        private System.Windows.Forms.Timer rainbowTimer;
        private System.Windows.Forms.Timer hotkeysLockedTimer;//Avoid hotkeys spamming

        public Color TitleBarColor;
        public Color MouseOverBackColor;
        public Color MenuStripBackColor;
        public Color MenuStripFontColor;
        public Color ButtonsBorderColor;
        private Color HandlerNoteBackColor;
        private Color HandlerNoteFontColor;
        private Color HandlerNoteMagnifierTitleBackColor;
        private Color HandlerNoteTitleFont;
        private Color StripMenuUpdateItemBack;
        private Color StripMenuUpdateItemFont;
        
        public FileInfo fontPath;

        private Label handlerUpdateLabel;
        private Label favoriteOnlyLabel;

        private PictureBox favoriteOnly;

        public Cursor hand_Cursor;
        public Cursor default_Cursor;
    
        public void SoundPlayer(string filePath)
        {
            splayer = new SoundPlayer(filePath);
            splayer.Play();
            splayer.Dispose();
        }

        private void controlscollect()
        {
            foreach (Control control in Controls)
            {
                ctrls.Add(control);
                foreach (Control container1 in control.Controls)
                {
                    ctrls.Add(container1);
                    foreach (Control container2 in container1.Controls)
                    {
                        ctrls.Add(container2);
                        foreach (Control container3 in container2.Controls)
                        {
                            ctrls.Add(container3);
                        }
                    }
                }
            }
        }

        public MainForm()
        {
            connected = Program.connected;
            Hub.Connected = connected;
            iconsIni = new IniFile(Path.Combine(Directory.GetCurrentDirectory() + "\\gui\\icons\\icons.ini"));
            Splash_On = bool.Parse(ini.IniReadValue("Dev", "SplashScreen_On"));
            DisableOfflineIcon = bool.Parse(ini.IniReadValue("Dev", "DisableOfflineIcon"));
            showFavoriteOnly = bool.Parse(ini.IniReadValue("Dev", "ShowFavoriteOnly"));
            mouseClick = bool.Parse(ini.IniReadValue("Dev", "MouseClick"));
            roundedcorners = bool.Parse(themeIni.IniReadValue("Misc", "UseRoundedCorners"));
            useButtonsBorder = bool.Parse(themeIni.IniReadValue("Misc", "UseButtonsBorder"));
            customFont = themeIni.IniReadValue("Font", "FontFamily");
            rgb_font = themeIni.IniReadValue("Colors", "Font").Split(',');
            rgb_MouseOverColor = themeIni.IniReadValue("Colors", "MouseOver").Split(',');
            rgb_MenuStripBackColor = themeIni.IniReadValue("Colors", "MenuStripBack").Split(',');
            rgb_MenuStripFontColor = themeIni.IniReadValue("Colors", "MenuStripFont").Split(',');
            rgb_TitleBarColor = themeIni.IniReadValue("Colors", "TitleBar").Split(',');
            rgb_HandlerNoteBackColor = themeIni.IniReadValue("Colors", "HandlerNoteBack").Split(',');
            rgb_HandlerNoteFontColor = themeIni.IniReadValue("Colors", "HandlerNoteFont").Split(',');
            rgb_HandlerNoteTitleFontColor = themeIni.IniReadValue("Colors", "HandlerNoteTitleFont").Split(',');
            rgb_ButtonsBorderColor = themeIni.IniReadValue("Colors", "ButtonsBorder").Split(',');
            rgb_HandlerNoteMagnifierTitleBackColor = themeIni.IniReadValue("Colors", "HandlerNoteMagnifierTitleBackColor ").Split(',');
            string[] windowSize = ini.IniReadValue("Misc", "WindowSize").Split('X');
            //string[] windowLocation = ini.IniReadValue("Misc", "WindowLocation").Split('X'); 
            disableFastHandlerUpdate = bool.Parse(ini.IniReadValue("Dev", "DisableFastHandlerUpdate"));
            float fontSize = float.Parse(themeIni.IniReadValue("Font", "MainFontSize"));
            bool coverBorderOff = bool.Parse(themeIni.IniReadValue("Misc", "DisableCoverBorder"));
            bool noteBorderOff = bool.Parse(themeIni.IniReadValue("Misc", "DisableNoteBorder"));

            TitleBarColor = Color.FromArgb(int.Parse(rgb_TitleBarColor[0]), int.Parse(rgb_TitleBarColor[1]), int.Parse(rgb_TitleBarColor[2]));
            MouseOverBackColor = Color.FromArgb(int.Parse(rgb_MouseOverColor[0]), int.Parse(rgb_MouseOverColor[1]), int.Parse(rgb_MouseOverColor[2]), int.Parse(rgb_MouseOverColor[3]));
            MenuStripBackColor = Color.FromArgb(int.Parse(rgb_MenuStripBackColor[0]), int.Parse(rgb_MenuStripBackColor[1]), int.Parse(rgb_MenuStripBackColor[2]));
            MenuStripFontColor = Color.FromArgb(int.Parse(rgb_MenuStripFontColor[0]), int.Parse(rgb_MenuStripFontColor[1]), int.Parse(rgb_MenuStripFontColor[2]));
            HandlerNoteBackColor = Color.FromArgb(int.Parse(rgb_HandlerNoteBackColor[0]), int.Parse(rgb_HandlerNoteBackColor[1]), int.Parse(rgb_HandlerNoteBackColor[2]));
            HandlerNoteFontColor = Color.FromArgb(int.Parse(rgb_HandlerNoteFontColor[0]), int.Parse(rgb_HandlerNoteFontColor[1]), int.Parse(rgb_HandlerNoteFontColor[2]));
            HandlerNoteMagnifierTitleBackColor = Color.FromArgb(int.Parse(rgb_HandlerNoteMagnifierTitleBackColor[0]), int.Parse(rgb_HandlerNoteMagnifierTitleBackColor[1]), int.Parse(rgb_HandlerNoteMagnifierTitleBackColor[2]));
            HandlerNoteTitleFont = Color.FromArgb(int.Parse(rgb_HandlerNoteTitleFontColor[0]), int.Parse(rgb_HandlerNoteTitleFontColor[1]), int.Parse(rgb_HandlerNoteTitleFontColor[2]));
            ButtonsBorderColor = Color.FromArgb(int.Parse(rgb_ButtonsBorderColor[0]), int.Parse(rgb_ButtonsBorderColor[1]), int.Parse(rgb_ButtonsBorderColor[2]));
         
            InitializeComponent();

            Size = new Size(int.Parse(windowSize[0]), int.Parse(windowSize[1]));
            //Location = PointToScreen(new Point(int.Parse(windowLocation[0]), int.Parse(windowLocation[1])));

            SuspendLayout();

            default_Cursor = new Cursor(theme + "cursor.ico");
            hand_Cursor = new Cursor(theme + "cursor_hand.ico");

            Cursor = default_Cursor;

            if (roundedcorners)
            {
                Region = Region.FromHrgn(GlobalWindowMethods.CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
                clientAreaPanel.Region = Region.FromHrgn(GlobalWindowMethods.CreateRoundRectRgn(0, 0, clientAreaPanel.Width, clientAreaPanel.Height, 20, 20));
            }

            BackColor = TitleBarColor;
            linksPanel.BackColor = BackColor;

            Font = new Font(customFont, fontSize, FontStyle.Regular, GraphicsUnit.Pixel, 0);
            ForeColor = Color.FromArgb(int.Parse(rgb_font[0]), int.Parse(rgb_font[1]), int.Parse(rgb_font[2]));
            scriptAuthorTxt.BackColor = HandlerNoteBackColor;
            scriptAuthorTxt.ForeColor = HandlerNoteFontColor;


            icons_Container.BackColor = Color.FromArgb(rightFrame.BackColor.A - rightFrame.BackColor.A, rightFrame.BackColor.R, rightFrame.BackColor.G, rightFrame.BackColor.B);
            inputsIconsDesc.BackColor = icons_Container.BackColor;
            HandlerNoteTitle.ForeColor = HandlerNoteTitleFont;

            scriptAuthorTxtSizer.BackColor = Color.FromArgb(int.Parse(themeIni.IniReadValue("Colors", "HandlerNoteContainerBackground").Split(',')[0]),
                                               int.Parse(themeIni.IniReadValue("Colors", "HandlerNoteContainerBackground").Split(',')[1]),
                                               int.Parse(themeIni.IniReadValue("Colors", "HandlerNoteContainerBackground").Split(',')[2]),
                                               int.Parse(themeIni.IniReadValue("Colors", "HandlerNoteContainerBackground").Split(',')[3]));

            buttonsBackColor = Color.FromArgb(int.Parse(themeIni.IniReadValue("Colors", "ButtonsBackground").Split(',')[0]),
                                               int.Parse(themeIni.IniReadValue("Colors", "ButtonsBackground").Split(',')[1]),
                                               int.Parse(themeIni.IniReadValue("Colors", "ButtonsBackground").Split(',')[2]),
                                               int.Parse(themeIni.IniReadValue("Colors", "ButtonsBackground").Split(',')[3]));

            btn_Play.ForeColor = Color.FromArgb(int.Parse(themeIni.IniReadValue("Colors", "PlayButtonFont").Split(',')[0]),
                                               int.Parse(themeIni.IniReadValue("Colors", "PlayButtonFont").Split(',')[1]),
                                               int.Parse(themeIni.IniReadValue("Colors", "PlayButtonFont").Split(',')[2]),
                                               int.Parse(themeIni.IniReadValue("Colors", "PlayButtonFont").Split(',')[3]));
           
            mainButtonFrame.BackColor = Color.FromArgb(int.Parse(themeIni.IniReadValue("Colors", "MainButtonFrameBackground").Split(',')[0]),
                                               int.Parse(themeIni.IniReadValue("Colors", "MainButtonFrameBackground").Split(',')[1]),
                                               int.Parse(themeIni.IniReadValue("Colors", "MainButtonFrameBackground").Split(',')[2]),
                                               int.Parse(themeIni.IniReadValue("Colors", "MainButtonFrameBackground").Split(',')[3]));

            rightFrame.BackColor = Color.FromArgb(int.Parse(themeIni.IniReadValue("Colors", "RightFrameBackground").Split(',')[0]),
                                               int.Parse(themeIni.IniReadValue("Colors", "RightFrameBackground").Split(',')[1]),
                                               int.Parse(themeIni.IniReadValue("Colors", "RightFrameBackground").Split(',')[2]),
                                               int.Parse(themeIni.IniReadValue("Colors", "RightFrameBackground").Split(',')[3]));         

            game_listSizer.BackColor = Color.FromArgb(int.Parse(themeIni.IniReadValue("Colors", "GameListBackground").Split(',')[0]),
                                               int.Parse(themeIni.IniReadValue("Colors", "GameListBackground").Split(',')[1]),
                                               int.Parse(themeIni.IniReadValue("Colors", "GameListBackground").Split(',')[2]),
                                               int.Parse(themeIni.IniReadValue("Colors", "GameListBackground").Split(',')[3]));

            StepPanel.BackColor = Color.FromArgb(int.Parse(themeIni.IniReadValue("Colors", "SetupScreenBackground").Split(',')[0]),
                                                int.Parse(themeIni.IniReadValue("Colors", "SetupScreenBackground").Split(',')[1]),
                                                int.Parse(themeIni.IniReadValue("Colors", "SetupScreenBackground").Split(',')[2]),
                                                int.Parse(themeIni.IniReadValue("Colors", "SetupScreenBackground").Split(',')[3]));

            StripMenuUpdateItemBack = Color.FromArgb(int.Parse(themeIni.IniReadValue("Colors", "StripMenuUpdateItemBack").Split(',')[0]),
                                              int.Parse(themeIni.IniReadValue("Colors", "StripMenuUpdateItemBack").Split(',')[1]),
                                              int.Parse(themeIni.IniReadValue("Colors", "StripMenuUpdateItemBack").Split(',')[2]),
                                              int.Parse(themeIni.IniReadValue("Colors", "StripMenuUpdateItemBack").Split(',')[3]));

            StripMenuUpdateItemFont = Color.FromArgb(int.Parse(themeIni.IniReadValue("Colors", "StripMenuUpdateItemFont").Split(',')[0]),
                                               int.Parse(themeIni.IniReadValue("Colors", "StripMenuUpdateItemFont").Split(',')[1]),
                                               int.Parse(themeIni.IniReadValue("Colors", "StripMenuUpdateItemFont").Split(',')[2]),
                                               int.Parse(themeIni.IniReadValue("Colors", "StripMenuUpdateItemFont").Split(',')[3]));

            clientAreaPanel.BackgroundImage = ImageCache.GetImage(theme + "background.jpg");
            btn_textSwitcher.BackgroundImage = ImageCache.GetImage(theme + "text_switcher.png");
            btnAutoSearch.BackColor = buttonsBackColor;
            button_UpdateAvailable.BackColor = buttonsBackColor;
            btnSearch.BackColor = buttonsBackColor;
            btn_gameOptions.BackColor = buttonsBackColor;
            btn_Download.BackColor = buttonsBackColor;
            btn_Play.BackColor = buttonsBackColor;
            btn_Extract.BackColor = buttonsBackColor;
            btn_gameOptions.BackgroundImage = ImageCache.GetImage(theme + "game_options.png");
            btn_Prev.BackgroundImage = ImageCache.GetImage(theme + "arrow_left.png");
            btn_Next.BackgroundImage = ImageCache.GetImage(theme + "arrow_right.png");
            coverFrame.BackgroundImage = ImageCache.GetImage(theme + "cover_layer.png");
            stepPanelPictureBox.Image = ImageCache.GetImage(theme + "logo.png");
            logo.BackgroundImage = ImageCache.GetImage(theme + "title_logo.png");
            btn_Discord.BackgroundImage = ImageCache.GetImage(theme + "discord.png");
            btn_downloadAssets.BackgroundImage = ImageCache.GetImage(theme + "title_download_assets.png");
            btn_faq.BackgroundImage = ImageCache.GetImage(theme + "faq.png");
            btn_Links.BackgroundImage = ImageCache.GetImage(theme + "title_dropdown_closed.png");
            btn_noHub.BackgroundImage = ImageCache.GetImage(theme + "title_no_hub.png");
            btn_reddit.BackgroundImage = ImageCache.GetImage(theme + "reddit.png");
            btn_SplitCalculator.BackgroundImage = ImageCache.GetImage(theme + "splitcalculator.png");
            btn_thirdPartytools.BackgroundImage = ImageCache.GetImage(theme + "thirdpartytools.png");
            closeBtn.BackgroundImage = ImageCache.GetImage(theme + "title_close.png");
            maximizeBtn.BackgroundImage = ImageCache.GetImage(theme + "title_maximize.png");
            minimizeBtn.BackgroundImage = ImageCache.GetImage(theme + "title_minimize.png");
            btn_settings.BackgroundImage = ImageCache.GetImage(theme + "title_settings.png");
            btn_dlFromHub.BackColor = buttonsBackColor;
            glowingLine0.Image = ImageCache.GetImage(theme + "lightbar_top.gif");
            btn_magnifier.Image = ImageCache.GetImage(theme + "magnifier.png");

            favorite_Unselected = ImageCache.GetImage(theme + "favorite_unselected.png");
            favorite_Selected = ImageCache.GetImage(theme + "favorite_selected.png");
        
            btn_Extract.FlatAppearance.MouseOverBackColor = MouseOverBackColor;
            btnAutoSearch.FlatAppearance.MouseOverBackColor = MouseOverBackColor;
            button_UpdateAvailable.FlatAppearance.MouseOverBackColor = MouseOverBackColor;
            btnSearch.FlatAppearance.MouseOverBackColor = MouseOverBackColor;
            btn_gameOptions.FlatAppearance.MouseOverBackColor = MouseOverBackColor;
            btn_Download.FlatAppearance.MouseOverBackColor = MouseOverBackColor;
            btn_Play.FlatAppearance.MouseOverBackColor = MouseOverBackColor;
            btn_Prev.FlatAppearance.MouseOverBackColor = MouseOverBackColor;
            btn_Next.FlatAppearance.MouseOverBackColor = MouseOverBackColor;
            btn_dlFromHub.FlatAppearance.MouseOverBackColor = MouseOverBackColor;
            gameContextMenuStrip.BackColor = MenuStripBackColor;
            gameContextMenuStrip.ForeColor = MenuStripFontColor;

            if (useButtonsBorder)
            {
                btnAutoSearch.FlatAppearance.BorderSize = 1;
                btnAutoSearch.FlatAppearance.BorderColor = ButtonsBorderColor;
                btnSearch.FlatAppearance.BorderSize = 1;
                btnSearch.FlatAppearance.BorderColor = ButtonsBorderColor;
                btn_gameOptions.FlatAppearance.BorderSize = 1;
                btn_gameOptions.FlatAppearance.BorderColor = ButtonsBorderColor;
                btn_Download.FlatAppearance.BorderSize = 1;
                btn_Download.FlatAppearance.BorderColor = ButtonsBorderColor;
                btn_Play.FlatAppearance.BorderSize = 1;
                btn_Play.FlatAppearance.BorderColor = ButtonsBorderColor;
                btn_Extract.FlatAppearance.BorderSize = 1;
                btn_Extract.FlatAppearance.BorderColor = ButtonsBorderColor;
                btn_Prev.FlatAppearance.BorderSize = 1;
                btn_Prev.FlatAppearance.BorderColor = ButtonsBorderColor;
                btn_Next.FlatAppearance.BorderSize = 1;
                btn_Next.FlatAppearance.BorderColor = ButtonsBorderColor;
                btn_dlFromHub.FlatAppearance.BorderSize = 1;
                btn_dlFromHub.FlatAppearance.BorderColor = ButtonsBorderColor;
            }
           
            linksPanel.Region = Region.FromHrgn(GlobalWindowMethods.CreateRoundRectRgn(0, 0, linksPanel.Width, linksPanel.Height, 15, 15));
            third_party_tools_container.Region = Region.FromHrgn(GlobalWindowMethods.CreateRoundRectRgn(0, 0, third_party_tools_container.Width, third_party_tools_container.Height, 10, 10));
            scriptAuthorTxtSizer.Region = Region.FromHrgn(GlobalWindowMethods.CreateRoundRectRgn(0, 0, scriptAuthorTxtSizer.Width, scriptAuthorTxtSizer.Height, 20, 20));

            btn_magnifier.Cursor = hand_Cursor;
            linkLabel1.Cursor = hand_Cursor;
            linkLabel2.Cursor = hand_Cursor;
            linkLabel3.Cursor = hand_Cursor;
            linkLabel4.Cursor = hand_Cursor;
            gameContextMenuStrip.Cursor = hand_Cursor;

            if (coverBorderOff)
            {
                cover.BorderStyle = BorderStyle.None;
            }

            if (noteBorderOff)
            {
                scriptAuthorTxtSizer.BorderStyle = BorderStyle.None;
            }

            controlscollect();

            foreach (Control control in ctrls)
            {
                if(control.Parent == this)
                {
                    control.BackColor = BackColor;//Title bar buttons => avoid "glitchs" while maximizing the window (aesthetic only)   
                }

                if (control.Name != "btn_Links" && control.Name != "btn_thirdPartytools" && control.Name != "HandlerNoteTitle" && control.Name != "scriptAuthorTxt")//Close "third_party_tools_container" control when an other control in the form is clicked.
                {
                    control.Font = new Font(customFont, fontSize, FontStyle.Regular, GraphicsUnit.Pixel, 0);
                    control.Click += new EventHandler(this.this_Click);
                }

                if (control.GetType() == typeof(Button))
                {
                    control.Cursor = hand_Cursor;
                }

                control.Click += new EventHandler(button_Click);

                if (mouseClick)
                {
                    handleClickSound(true);
                }
            }

#if DEBUG
            txt_version.ForeColor = Color.LightSteelBlue;
            txt_version.Text = "DEBUG " + version;
#else
            if (bool.Parse(themeIni.IniReadValue("Misc", "HideVersion")) == false)
            {
                txt_version.Text = version;
            }
            else
            {
                txt_version.Text = "";
            }
#endif
            ResumeLayout();

            minimizeBtn.Click += new EventHandler(this.minimizeButton);
            maximizeBtn.Click += new EventHandler(this.maximizeButton);
            closeBtn.Click += new EventHandler(this.closeButton);

            defBackground = clientAreaPanel.BackgroundImage as Bitmap;

            positionsControl = new PositionsControl();

            positionsControl.textZoomContainer.BackColor = HandlerNoteMagnifierTitleBackColor;
            positionsControl.handlerNoteZoom.BackColor = HandlerNoteBackColor;
            positionsControl.handlerNoteZoom.ForeColor = HandlerNoteFontColor;
            positionsControl.profileSettings_btn.Click += new EventHandler(this.ProfileSettings_btn_Click);
            positionsControl.gameProfilesList_btn.Click += new EventHandler(this.gameProfilesList_btn_Click);
            positionsControl.OnCanPlayUpdated += StepCanPlay;
            positionsControl.Click += new EventHandler(this_Click);
            positionsControl.btn_Play = btn_Play;

            settings = new NewSettings(this, positionsControl);
            profileSettings = new ProfileSettings(this, positionsControl);

            searchDisksForm = new SearchDisksForm(this);
            clientAreaPanel.Controls.Add(settings);
            clientAreaPanel.Controls.Add(profileSettings);
            clientAreaPanel.Controls.Add(searchDisksForm);
            positionsControl.Paint += PositionsControl_Paint;

            settings.RegHotkeys(this);

            controls = new Dictionary<UserGameInfo, GameControl>();
            gameManager = new GameManager(this);
            assetsDownloader = new AssetsDownloader();
            optionsControl = new PlayerOptionsControl();
            jsControl = new JSUserInputControl();

            optionsControl.OnCanPlayUpdated += StepCanPlay;
            jsControl.OnCanPlayUpdated += StepCanPlay;

            scriptDownloader = new ScriptDownloader(this);
            downloadPrompt = new DownloadPrompt(handler, this, null, true);
            Xinput_S_Setup = new XInputShortcutsSetup();

            favoriteOnlyLabel = new Label
            {
                AutoSize = true,
                Text = "Favorite Games",
                BackColor = buttonsBackColor,
                ForeColor = this.ForeColor,
            };

            favoriteOnly = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent,
                Cursor = hand_Cursor
            };

            favoriteOnly.Image = showFavoriteOnly ? favorite_Selected : favorite_Unselected;
            favoriteOnly.Click += new EventHandler(FavoriteOnly_Click);

            mainButtonFrame.Controls.Add(favoriteOnlyLabel);
            mainButtonFrame.Controls.Add(favoriteOnly);

            hotkeysLockedTimer = new System.Windows.Forms.Timer();
            hotkeysLockedTimer.Tick += new EventHandler(hotkeysLockedTimerTick);

            gameContextMenuStrip.Renderer = new MyRenderer();

            RefreshGames(true);
            CenterToScreen();

            //Enable only for windows version with default support for xinput1.4.dll ,
            //might be fixable by placing the dll at the root of our exe but not for now.
            string windowsVersion = MachineSpecs.GetPCspecs(null);
            if (!windowsVersion.Contains("Windows 7") &&
                !windowsVersion.Contains("Windows Vista"))
            {
                ControllersShortcuts.ctrlsShortcuts = new Thread(ControllersShortcuts.StartSRTCThread);
                ControllersShortcuts.ctrlsShortcuts.Priority = ThreadPriority.Lowest;
                ControllersShortcuts.ctrlsShortcuts.Start();
                ControllersShortcuts.UpdateShortcutsValue();

                ControllersUINav.controllersUINavThread = new Thread(ControllersUINav.StartXConNavThread);
                ControllersUINav.controllersUINavThread.Start();
                ControllersUINav.UpdateUINavSettings();
            }
            else
            {
                NewSettings._ctrlr_shorcuts.Text = "Windows 8™ and up only";
                NewSettings._ctrlr_shorcuts.Enabled = false;
            }

            DPIManager.Register(this);
            DPIManager.AddForm(this);
        }

        public Thread _ControllersShortcuts = ControllersShortcuts.ctrlsShortcuts;

        private void FavoriteOnly_Click(object sender, EventArgs e)
        {
            bool selected = favoriteOnly.Image.Equals(favorite_Selected);

            if (selected)
            {
                favoriteOnly.Image = favorite_Unselected;
                showFavoriteOnly = false;
            }
            else
            {
                favoriteOnly.Image = favorite_Selected;
                showFavoriteOnly = true;
            }

            ini.IniWriteValue("Dev", "ShowFavoriteOnly", showFavoriteOnly.ToString());         
            RefreshGames(false);
        }

        public new void UpdateSize(float scale)
        {
            if (IsDisposed)
            {
                DPIManager.Unregister(this);
                return;
            }

            SuspendLayout();

            float newFontSize = Font.Size * scale;
            float mainButtonFrameFont = mainButtonFrame.Font.Size * 1.0f;

            if (scale > 1.0f)
            {
                foreach (Control button in mainButtonFrame.Controls)
                {
                    button.Font = new Font(customFont, mainButtonFrameFont, FontStyle.Regular, GraphicsUnit.Pixel, 0);
                }
            }

            gameContextMenuStrip.Font = new Font(gameContextMenuStrip.Font.FontFamily, 10.25f, FontStyle.Regular, GraphicsUnit.Pixel, 0);
            btn_Play.Font = new Font(customFont, mainButtonFrameFont, FontStyle.Bold, GraphicsUnit.Pixel, 0);
            scriptAuthorTxt.Font = new Font(customFont, newFontSize, FontStyle.Regular, GraphicsUnit.Pixel, 0);
            scriptAuthorTxt.Size = new Size((int)(189 * scale), (int)(191 * scale));
            favoriteOnlyLabel.Font = new Font(customFont, mainButtonFrameFont, FontStyle.Regular, GraphicsUnit.Pixel, 0);
            favoriteOnlyLabel.Location = new Point(1, mainButtonFrame.Height / 2 - (favoriteOnlyLabel.Height / 2)/* * (int)scale*/);
            favoriteOnly.Size = new Size(favoriteOnlyLabel.Height, favoriteOnlyLabel.Height);
            float favoriteY = favoriteOnlyLabel.Right + (5 * scale);
            favoriteOnly.Location = new Point((int)(favoriteY), mainButtonFrame.Height / 2 - (favoriteOnly.Height / 2) /** (int)scale*/);

            ResumeLayout();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            DisposeTimer = new System.Windows.Forms.Timer();
            DisposeTimer.Interval = (2900); //millisecond
            DisposeTimer.Tick += new EventHandler(MainTimerTick);
            DisposeTimer.Start();

            if (Splash_On)
            {
                splashscreen.Show();
            }

            DPIManager.ForceUpdate();
        }

        public void handleClickSound(bool enable)
        {
            mouseClick = enable;
        }

        private void MainTimerTick(Object Object, EventArgs EventArgs)
        {
            if (Splash_On)
            {
                splashscreen.Dispose();
            }

            if (connected)
            {
                btn_noHub.Visible = false;
                btn_downloadAssets.Enabled = true;
                btn_Download.Enabled = true;
                DisposeTimer.Dispose();
            }
            else
            {
                if (!DisableOfflineIcon) { btn_noHub.Visible = true; }
                btn_downloadAssets.Enabled = false;
                btn_Download.Enabled = false;
            }
        }

        private void TriggerHubShowCase()
        {
            hubShowcase = new HubShowcase(this);
            hubShowcase.Location = new Point(game_listSizer.Right, mainButtonFrame.Bottom + (clientAreaPanel.Height / 2 - hubShowcase.Height / 2));
            hubShowcase.Size = new Size(hubShowcase.Width - 40, hubShowcase.Height);
            hubShowcase.Visible = true;
            stepPanelPictureBox.Visible = false;
            clientAreaPanel.Controls.Add(hubShowcase);
        }

        private void PositionsControl_Paint(object sender, PaintEventArgs e)
        {
            if (positionsControl.isDisconnected)
            {
                DPIManager.ForceUpdate();
                positionsControl.isDisconnected = false;
            }
        }

        protected override Size DefaultSize => new Size(1050, 701);

        protected override void WndProc(ref Message m)
        {
            const int RESIZE_HANDLE_SIZE = 10;

            if (this.WindowState == FormWindowState.Normal)
            {
                switch (m.Msg)//resizing messages handling
                {
                    case 0x0084/*NCHITTEST*/ :
                        base.WndProc(ref m);

                        if ((int)m.Result == 0x01/*HTCLIENT*/)
                        {
                            Point screenPoint = new Point(m.LParam.ToInt32());
                            Point clientPoint = this.PointToClient(screenPoint);

                            if (clientPoint.Y <= RESIZE_HANDLE_SIZE)
                            {
                                if (clientPoint.X <= RESIZE_HANDLE_SIZE)
                                    m.Result = (IntPtr)13/*HTTOPLEFT*/ ;
                                else if (clientPoint.X < (Size.Width - RESIZE_HANDLE_SIZE))
                                    m.Result = (IntPtr)12/*HTTOP*/ ;
                                else
                                    m.Result = (IntPtr)14/*HTTOPRIGHT*/ ;
                            }
                            else if (clientPoint.Y <= (Size.Height - RESIZE_HANDLE_SIZE))
                            {
                                if (clientPoint.X <= RESIZE_HANDLE_SIZE)
                                    m.Result = (IntPtr)10/*HTLEFT*/ ;
                                else if (clientPoint.X < (Size.Width - RESIZE_HANDLE_SIZE))
                                    m.Result = (IntPtr)2/*HTCAPTION*/ ;
                                else
                                    m.Result = (IntPtr)11/*HTRIGHT*/ ;
                            }
                            else
                            {
                                if (clientPoint.X <= RESIZE_HANDLE_SIZE)
                                    m.Result = (IntPtr)16/*HTBOTTOMLEFT*/ ;
                                else if (clientPoint.X < (Size.Width - RESIZE_HANDLE_SIZE))
                                    m.Result = (IntPtr)15/*HTBOTTOM*/ ;
                                else
                                    m.Result = (IntPtr)17/*HTBOTTOMRIGHT*/ ;
                            }
                        }

                        return;
                }
            }

            Point cursorPos = PointToClient(Cursor.Position);
            Rectangle inRect = new Rectangle(10, 10, Width - 20, Height - 20);

            if (!inRect.Contains(cursorPos))
            {
                canResize = true;
            }
            else
            {
                if (Cursor.Current != default_Cursor)
                {
                    Cursor.Current = default_Cursor;
                }

                canResize = false;
            }

            if (!canResize)
            {
                if (m.Msg == 0x020)//Do not reset custom cursor when the mouse hover over the Form background(needed because of the custom resizing/moving messages handling) 
                {
                    m.Result = IntPtr.Zero;
                    return;
                }
            }

            if (m.Msg == 0x00FF)//WM_INPUT
            {
                RawInputAction(m.LParam);
            }
            else if (m.Msg == 0x0312 && m.WParam.ToInt32() == KillProcess_HotkeyID)
            {
                if (!Gaming.Coop.InputManagement.LockInput.IsLocked)
                {
                    if (hotkeysLocked)
                    {
                        return;
                    }

                    TriggerOSD(2000, "See You Later!");
                    User32Util.ShowTaskBar();
                    Close();
                }
                else
                {
                    TriggerOSD(1600, $"Unlock Inputs First (Press {ini.IniReadValue("Hotkeys", "LockKey")} key)");
                }
            }
            else if (m.Msg == 0x0312 && m.WParam.ToInt32() == TopMost_HotkeyID)
            {

                if (hotkeysLocked || I_GameHandler == null)
                {
                    return;
                }

                GlobalWindowMethods.ShowHideWindows(currentGame);
            }
            else if (m.Msg == 0x0312 && m.WParam.ToInt32() == StopSession_HotkeyID)
            {
                if (!Gaming.Coop.InputManagement.LockInput.IsLocked)
                {
                    if (hotkeysLocked || I_GameHandler == null)
                    {
                        return;
                    }

                    if (btn_Play.Text == "S T O P")
                    {
                        btn_Play.PerformClick();
                    }

                    TriggerOSD(2000, "Session Ended");
                }
                else
                {
                    TriggerOSD(1600, $"Unlock Inputs First (Press {ini.IniReadValue("Hotkeys", "LockKey")} key)");
                }
            }
            else if (m.Msg == 0x0312 && m.WParam.ToInt32() == SetFocus_HotkeyID)
            {
                if (!Gaming.Coop.InputManagement.LockInput.IsLocked)
                {
                    if (hotkeysLocked || I_GameHandler == null)
                    {
                        return;
                    }

                    GlobalWindowMethods.ChangeForegroundWindow();
                    TriggerOSD(2000, "Game Windows Unfocused");
                    stepPanelPictureBox.Focus();
                }
                else
                {
                    TriggerOSD(1600, $"Unlock Inputs First (Press {ini.IniReadValue("Hotkeys", "LockKey")} key)");
                }
            }
            else if (m.Msg == 0x0312 && m.WParam.ToInt32() == ResetWindows_HotkeyID)
            {
                if (!Gaming.Coop.InputManagement.LockInput.IsLocked)
                {
                    if (hotkeysLocked || I_GameHandler == null)
                    {
                        return;
                    }

                    I_GameHandler.Update(currentGame.HandlerInterval, true);
                }
                else
                {
                    TriggerOSD(1600, $"Unlock Inputs First (Press {ini.IniReadValue("Hotkeys", "LockKey")} key)");
                }
            }
            else if (m.Msg == 0x0312 && m.WParam.ToInt32() == Cutscenes_HotkeyID)
            {
                if (hotkeysLocked || I_GameHandler == null)
                {
                    return;
                }

                if (!ToggleCutscenes)
                {
                    GlobalWindowMethods.ToggleCutScenesMode(true);
                    ToggleCutscenes = true;
                }
                else
                {
                    GlobalWindowMethods.ToggleCutScenesMode(false);
                    ToggleCutscenes = false;
                }
            }
            else if (m.Msg == 0x0312 && m.WParam.ToInt32() == Switch_HotkeyID)
            {
                if (!Gaming.Coop.InputManagement.LockInput.IsLocked)
                {
                    if (hotkeysLocked || I_GameHandler == null)
                    {
                        return;
                    }

                    GlobalWindowMethods.SwitchLayout();
                }
                else
                {
                    TriggerOSD(1600, $"Unlock Inputs First (Press {ini.IniReadValue("Hotkeys", "LockKey")} key)");
                }
            }

            base.WndProc(ref m);
        }

        public void TriggerOSD(int timerMS, string text)
        {
            if (!hotkeysLocked)
            {
                hotkeysLockedTimer.Stop();
                hotkeysLocked = true;
                hotkeysLockedTimer.Interval = (timerMS); //millisecond
                hotkeysLockedTimer.Start();

                Globals.MainOSD.Settings(timerMS, Color.YellowGreen, text);
            }
        }

        private void hotkeysLockedTimerTick(Object Object, EventArgs EventArgs)
        {
            hotkeysLocked = false;
            hotkeysLockedTimer.Stop();
        }

        public void RefreshGames(bool checkUpdate)
        {
            List<UserGameInfo> games;

            lock (controls)
            {
                foreach (KeyValuePair<UserGameInfo, GameControl> con in controls)
                {
                    if (con.Value != null)
                    {
                        con.Value.Dispose();
                    }
                }

                list_Games.Controls.Clear();
                controls.Clear();

                games = gameManager.User.Games;

                if (games.Count == 0)
                {
                    noGamesPresent = true;
                    GameControl con = new GameControl(null, null, false, false)
                    {
                        Width = game_listSizer.Width,
                        Text = "No games",
                        Font = this.Font,
                    };
                
                    list_Games.Controls.Add(con);
                }
                else
                {
                    for (int i = 0; i < games.Count; i++)
                    {
                        UserGameInfo game = games[i];
                        NewUserGame(game, checkUpdate);
                    }
                }
            }

            GameManager.Instance.SaveUserProfile();
        }

        public void NewUserGame(UserGameInfo game, bool checkUpdate)
        {
            if (game.Game == null || !game.IsGamePresent())
            {
                return;
            }

            if (noGamesPresent)
            {
                noGamesPresent = false;
                RefreshGames(false);
                return;
            }

            list_Games.SuspendLayout();

            bool updateAvailable = game.Game.UpdateAvailable;

            bool favorite = game.Favorite;

            if (!disableFastHandlerUpdate && connected && checkUpdate)
            {
                updateAvailable = game.Game.IsUpdateAvailable(true);//game.Game.UpdateAvailable;
                game.Game.UpdateAvailable = updateAvailable;
            }

            GameControl con = new GameControl(game.Game, game, updateAvailable, favorite)
            {
                Width = game_listSizer.Width,
            };

            if (showFavoriteOnly)
            {
                if (favorite)
                {
                    controls.Add(game, con);
                    list_Games.Controls.Add(con);
                    ThreadPool.QueueUserWorkItem(GetIcon, game);
                }
            }
            else
            {
                controls.Add(game, con);
                list_Games.Controls.Add(con);
                ThreadPool.QueueUserWorkItem(GetIcon, game);
            }

            list_Games.ResumeLayout();
        }

        private void RefreshUI()
        {
            SuspendLayout();
            rightFrame.Visible = false;
            StepPanel.Visible = false;
            clientAreaPanel.BackgroundImage = defBackground;
            stepPanelPictureBox.Visible = true;
            cover.BackgroundImage?.Dispose();
            rainbowTimer?.Dispose();
            rainbowTimerRunning = false;
            hubShowcase?.Dispose();

            if (currentControl != null)
            {
                RefreshGames(false);
            }

            ResumeLayout();
        }

        private void GetIcon(object state)
        {
            UserGameInfo game = (UserGameInfo)state;
            Bitmap bmp = null;
            string iconPath = iconsIni.IniReadValue("GameIcons", game.Game.GameName);

            if (!string.IsNullOrEmpty(iconPath))
            {
                if (iconPath.EndsWith(".exe"))
                {
                    bmp = ImageCache.GetImage(Path.Combine(Directory.GetCurrentDirectory() + "\\gui\\icons\\default.png"));
                    Icon icon = Shell32.GetIcon(iconPath, false);
                    bmp = icon.ToBitmap();
                    icon.Dispose();
                }
                else
                {
                    if (File.Exists(iconPath))
                    {
                        bmp = ImageCache.GetImage(iconPath);
                    }
                    else
                    {
                        if (File.Exists(Path.Combine(Directory.GetCurrentDirectory() + "\\gui\\icons\\default.png")))
                        {
                            bmp = ImageCache.GetImage(Path.Combine(Directory.GetCurrentDirectory() + "\\gui\\icons\\default.png"));
                        }
                    }
                }
            }
            else
            {
                Icon icon = Shell32.GetIcon(game.ExePath, false);
                bmp = icon.ToBitmap();
                icon.Dispose();
            }

            game.Icon = bmp;

            lock (controls)
            {
                if (controls.ContainsKey(game))
                {
                    GameControl control = controls[game];
                    control.Invoke((Action)delegate ()
                    {
                        control.Click += new EventHandler(button_Click);
                        control.MouseMove += new MouseEventHandler(gameControl_MouseMove);
                        control.MouseEnter += new EventHandler(gameControl_MouseEnter);
                        control.MouseLeave += new EventHandler(gameControl_MouseLeave);
                        control.Image = game.Icon;
                    });
                }
            }
        }

        private void gameControl_MouseEnter(object sender, EventArgs e)
        {
            GameControl c = sender as GameControl;

            if (c.updateAvailable)
            {
                handlerUpdateLabel = new Label
                {
                    Name = "UpdateLabel",
                    AutoSize = true,
                    BackColor = Color.FromArgb(190, 0, 0, 0),
                    ForeColor = Color.White,
                    Font = new Font(customFont, 7.25F),
                    Text = "There is an update available for this handler,\nright click and select \"Update Handler\" \nto quickly download the latest version.",
                    BorderStyle = BorderStyle.FixedSingle
                };

                handlerUpdateLabel.Location = PointToClient(new Point(MousePosition.X + 15, MousePosition.Y - c.Height));
                clientAreaPanel.Controls.Add(handlerUpdateLabel);
                handlerUpdateLabel.BringToFront();
            }
        }

        private void gameControl_MouseMove(object sender, EventArgs e)
        {
            GameControl c = sender as GameControl;
             
            if (handlerUpdateLabel != null)
            {
                handlerUpdateLabel.Location = PointToClient(new Point(MousePosition.X + 15, MousePosition.Y - c.Height));
            }
        }

        private void gameControl_MouseLeave(object sender, EventArgs e)
        {
            foreach (Control updateLabel in clientAreaPanel.Controls)//Use this because for some reasons sometimes the label won't dispose.
            {
                if (updateLabel.Name == "UpdateLabel")
                    updateLabel.Dispose();
                break;
            }
        }

        private void btn_downloadAssets_Click(object sender, EventArgs e)
        {
            if (gameManager.User.Games.Count == 0)
            {
                TriggerOSD(1600, $"Add Game(s) In Your List");
                return;
            }
           
            assetsDownloader.DownloadGameAssets(this, gameManager, scriptDownloader, currentControl);
        }

        private int r = 0;
        private int g = 0;
        private int b = 0;
        private bool loop = false;

        private void rainbowTimerTick(Object Object, EventArgs EventArgs)
        {
            if (HandlerNoteTitle.Text == "Handler Notes" || HandlerNoteTitle.Text == "Read First")
            {
                if (!loop)
                {
                    HandlerNoteTitle.Text = "Handler Notes";
                    if (r < 255 && b < 255) { r += 3; b += 3; };
                    if (b >= 255 && r >= 255)
                        loop = true;
                    HandlerNoteTitle.Font = new Font(HandlerNoteTitle.Font.FontFamily, HandlerNoteTitle.Font.Size, FontStyle.Bold);
                }
                else
                {
                    HandlerNoteTitle.Text = "Read First";
                    if (r > 0 && b > 0) { r -= 3; b -= 3; }
                    if (b <= 0 && r <= 0)
                        loop = false;
                }

                HandlerNoteTitle.ForeColor = Color.FromArgb(r, 255, b);

            }
            else if (HandlerNoteTitle.Text.Contains("Profile n°"))
            {
                HandlerNoteTitle.ForeColor = Color.LightGreen;
                HandlerNoteTitle.Font = new Font(HandlerNoteTitle.Font.FontFamily, HandlerNoteTitle.Font.Size, FontStyle.Bold);
            }
            else
            {
                HandlerNoteTitle.ForeColor = HandlerNoteTitleFont;
                HandlerNoteTitle.Font = new Font(HandlerNoteTitle.Font.FontFamily, (float)HandlerNoteTitle.Font.Size, FontStyle.Regular);
            }
        }

        private bool rainbowTimerRunning = false;

        private void list_Games_SelectedChanged(object arg1, Control arg2)
        {
            currentControl = (GameControl)arg1;
            currentGameInfo = currentControl.UserGameInfo;

            if (currentGameInfo != null)
            {
                if (!CheckGameRequirements.MatchRequirements(currentGameInfo.Game))
                {
                    RefreshUI();
                    return;
                }
            }

            if (profileSettings.Visible)
            {
                profileSettings.Visible = false;
            }

            positionsControl.handlerNoteZoom.Visible = false;
            btn_magnifier.Image = ImageCache.GetImage(theme + "magnifier.png");
            btn_textSwitcher.Visible = false;

            screenshotImg?.Dispose();
            coverImg?.Dispose();

            if (currentGameInfo == null)
            {
                btn_gameOptions.Visible = false;
                button_UpdateAvailable.Visible = false;
                return;
            }
            else
            {
                currentGame = currentGameInfo.Game;
                currentGameSetup = currentControl.UserGameInfo.Game.GameName;

                button_UpdateAvailable.Visible = currentGameInfo.Game.UpdateAvailable;
                HandlerNoteTitle.Text = "Handler Notes";
                hubShowcase?.Dispose();

                if (!disableGameProfiles)
                {
                    positionsControl.profileSettings_btn.Visible = true;
                    positionsControl.gameProfilesList.Visible = false;
                    positionsControl.gameProfilesList_btn.Visible = true;
                    positionsControl.gameProfilesList_btn.Image = ImageCache.GetImage(theme + "profiles_list.png");
                }
                else
                {
                    positionsControl.profileSettings_btn.Visible = false;
                    positionsControl.gameProfilesList.Visible = false;
                    positionsControl.gameProfilesList_btn.Visible = false;
                }

                if (!rainbowTimerRunning)
                {
                    rainbowTimer = new System.Windows.Forms.Timer();
                    rainbowTimer.Interval = (25); //millisecond                   
                    rainbowTimer.Tick += new EventHandler(rainbowTimerTick);
                    rainbowTimer.Start();
                    rainbowTimerRunning = true;
                }

                InputIcons.SetInputsIcons(this, currentGame);
                SetBackroundAndCover.ApplyBackgroundAndCover(this, currentControl.UserGameInfo.GameGuid);
                rightFrame.Visible = true;

                btn_Play.Enabled = false;
                
                btn_gameOptions.Visible = true;
                StepPanel.Visible = true;
                positionsControl.textZoomContainer.Visible = false;
                stepPanelPictureBox.Visible = false;

                stepsList = new List<UserInputControl>
                {
                   positionsControl,
                   optionsControl
                };

                for (int i = 0; i < currentGame.CustomSteps.Count; i++)
                {
                    stepsList.Add(jsControl);
                }

                currentProfile = new GameProfile();
                GameProfile.GameGUID = currentGame.GUID;
                currentProfile.InitializeDefault(currentGame, positionsControl);
                gameManager.UpdateCurrentGameProfile(currentProfile);

                if (!disableGameProfiles)
                {
                    ProfilesList.profilesList.Update_ProfilesList();
                    gameProfilesList_btn_Click(null, null);//Show profiles list 
                    positionsControl.gameProfilesList_btn.Visible = GameProfile.profilesPathList.Count > 0;
                    ProfilesList.profilesList.Locked = false;
                }

                btn_gameOptions.Enabled = true;
               
                btn_textSwitcher.Visible = File.Exists(Path.Combine(Application.StartupPath, $"gui\\descriptions\\{currentGame.GUID}.txt"));

                if (currentGame.Description?.Length > 0)
                {
                    scriptAuthorTxt.Text = currentGame.Description;
                    scriptAuthorTxtSizer.Visible = true;
                }
                else if (File.Exists(Path.Combine(Application.StartupPath, $"gui\\descriptions\\{currentGame.GUID}.txt")))
                {
                    StreamReader desc = new StreamReader(Path.Combine(Application.StartupPath, $"gui\\descriptions\\{currentGame.GUID}.txt"));

                    HandlerNoteTitle.Text = "Game Description";
                    scriptAuthorTxt.Text = desc.ReadToEnd();
                    btn_textSwitcher.Visible = false;
                    desc.Dispose();
                }
                else
                {
                    scriptAuthorTxtSizer.Visible = false;
                    scriptAuthorTxt.Text = "";
                }

                content?.Dispose();

                if (!currentGameInfo.KeepSymLink)
                {
                    string path = Path.Combine(gameManager.GetAppContentPath(), currentGameInfo.Game.GUID);
                    CleanGameContent.CleanContentFolder(path, currentGame);
                }

                // content manager is shared within the same game
                content = new ContentManager(currentGame);

                GoToStep(0);
            }
        }

        private void EnablePlay()
        {
            btn_Play.Enabled = true;
        }

        private void StepCanPlay(UserControl obj, bool canProceed, bool autoProceed)
        {
            if (btn_Prev.Enabled)
            {
                btn_Prev.BackgroundImage = ImageCache.GetImage(theme + "arrow_left_mousehover.png");
            }
            else
            {
                btn_Prev.BackgroundImage = ImageCache.GetImage(theme + "arrow_left.png");
            }

            if (!canProceed)
            {
                btn_Prev.Enabled = false;
                if (btn_Play.Text == "PLAY" || btn_Next.Enabled)
                {
                    btn_Play.Enabled = false;
                }

                btn_Next.Enabled = false;
                btn_Next.BackgroundImage = ImageCache.GetImage(theme + "arrow_right.png");
                btn_Prev.BackgroundImage = ImageCache.GetImage(theme + "arrow_left.png");
                return;
            }
            else
            {
                btn_Next.BackgroundImage = ImageCache.GetImage(theme + "arrow_right.png");
            }

            if (currentGame.Options.Count == 0)
            {
                EnablePlay();
                return;
            }

            if (currentStepIndex + 1 > stepsList.Count - 1)
            {
                EnablePlay();
                return;
            }
            else
            {
                if (btn_Play.Text == "PLAY")
                {
                    btn_Play.Enabled = false;
                }
                else
                {
                    btn_Play.Enabled = true;
                }
            }

            if (autoProceed)
            {
                GoToStep(currentStepIndex + 1);
            }
            else
            {
                btn_Next.Enabled = true;
                btn_Next.BackgroundImage = ImageCache.GetImage(theme + "arrow_right_mousehover.png");
            }
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            GoToStep(currentStepIndex + 1);
        }

        private void KillCurrentStep()
        {
            foreach (Control c in StepPanel.Controls)
            {
                if (!c.Name.Equals("scriptAuthorTxtSizer"))
                {
                    StepPanel.Controls.Remove(c);
                }
            }
        }

        private void GoToStep(int step)
        {
            btn_Prev.Enabled = step > 0;

            if (step >= stepsList.Count)
            {
                return;
            }

            if (step >= 2)
            {
                // Custom steps
                List<CustomStep> customSteps = currentGame.CustomSteps;
                int customStepIndex = step - 2;
                CustomStep customStep = customSteps[0];

                if (customStep.UpdateRequired != null)
                {
                    customStep.UpdateRequired();
                }

                if (customStep.Required)
                {
                    jsControl.CustomStep = customStep;
                    jsControl.Content = content;
                }
                else
                {
                    EnablePlay();
                    return;
                }
            }

            KillCurrentStep();

            if (GameProfile.Ready)
            {
                if (currentGame.CustomSteps.Count > 0)
                {
                    jsControl.CustomStep = currentGame.CustomSteps[0];
                    jsControl.Content = content;

                    currentStepIndex = stepsList.Count - 1;
                    currentStep = stepsList[stepsList.Count - 1];
                    currentStep.Size = StepPanel.Size;
                    currentStep.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
                    currentStep = stepsList[stepsList.Count - 1];
                    currentStep.Initialize(currentGameInfo, currentProfile);

                    StepPanel.Controls.Add(currentStep);
                    label_StepTitle.Text = currentStep.Title;

                    btn_Next.Enabled = currentStep.CanProceed && step != stepsList.Count - 1;

                    if (GameProfile.AutoPlay)
                    {
                        EnablePlay();
                    }
                    return;
                }
            }

            currentStepIndex = step;
            currentStep = stepsList[step];
            currentStep.Size = StepPanel.Size;
            currentStep.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
            currentStep.Initialize(currentGameInfo, currentProfile);

            StepPanel.Controls.Add(currentStep);
            label_StepTitle.Text = currentStep.Title;

            btn_Next.Enabled = currentStep.CanProceed && step != stepsList.Count - 1;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            formClosing = true;

            if (I_GameHandler != null)
            {
                Log("OnFormClosed method calling Handler End function");
                try
                {
                    I_GameHandler.End(false);
                }
                catch { }
            }

            User32Util.ShowTaskBar();

            if (!restartRequired)
            {
                Process.GetCurrentProcess().Kill();
            }
        }

        private void btn_Play_Click(object sender, EventArgs e)
        {
            bool useCustomLayout = GameProfile.UseSplitDiv;

            if (btn_Play.Text == "S T O P")
            {
                try
                {
                    if (I_GameHandler != null)
                    {
                        Log("Stop button clicked, calling Handler End function");
                        I_GameHandler.End(true);
                    }
                }
                catch { }

                GameProfile.currentProfile.Reset();
                positionsControl.gamepadTimer = new System.Threading.Timer(positionsControl.GamepadTimer_Tick, null, 0, 1000);
                positionsControl.gamepadPollTimer = new System.Threading.Timer(positionsControl.GamepadPollTimer_Tick, null, 0, 1001);

                return;
            }

            currentStep?.Ended();

            btn_Play.Text = "S T O P";

            btn_Prev.Enabled = false;

            gameManager.AddScript(Path.GetFileNameWithoutExtension(currentGame.JsFileName));

            currentGame = gameManager.GetGame(currentGameInfo.ExePath);
            currentGameInfo.InitializeDefault(currentGame, currentGameInfo.ExePath);

            I_GameHandler = gameManager.MakeHandler(currentGame);
            I_GameHandler.Initialize(currentGameInfo, GameProfile.CleanClone(currentProfile));
            I_GameHandler.Ended += handler_Ended;

            GameProfile.Game = currentGame;
            gameManager.Play(I_GameHandler);

            if (I_GameHandler.TimerInterval > 0)
            {
                handlerThread = new Thread(UpdateGameManager);
                handlerThread.Start();
            }

            if (currentGame.HideTaskbar && !useCustomLayout)
            {
                User32Util.HideTaskbar();
            }

            if (currentGame.ProtoInput.AutoHideTaskbar || useCustomLayout)
            {
                if (ProtoInput.protoInput.GetTaskbarAutohide())
                {
                    currentGame.ProtoInput.AutoHideTaskbar = false; // If already hidden don't change it, and dont set it unhidden after.
                }
                else
                {
                    ProtoInput.protoInput.SetTaskbarAutohide(true);
                }
            }

            if (profileSettings.Visible)
            {
                profileSettings.Visible = false;
            }

            if (btn_Play.ContainsFocus)
            {
                stepPanelPictureBox.Focus();
            }

            if (!currentGame.ToggleUnfocusOnInputsLock)//Not sure
            {
                WindowState = FormWindowState.Minimized;
            }

            RefreshUI();
        }

        private void SetBtnToPlay()
        {
            btn_Play.Text = "PLAY";
        }

        private void handler_Ended()
        {
            Log("Handler ended method called");
            User32Util.ShowTaskBar();
            I_GameHandler = null;

            try
            {
                if (handlerThread != null)
                {
                    handlerThread.Abort();
                    handlerThread = null;
                }
            }
            catch { }

            this.Invoke((MethodInvoker)delegate ()
            {
                btn_Play.Text = "PLAY";
                btn_Play.Enabled = false;
                currentControl = null;

                User32Util.ShowTaskBar();
                GoToStep(0);
                RefreshGames(false);
                WindowState = FormWindowState.Normal;

                BringToFront();

                stepPanelPictureBox.Focus();
                positionsControl.gamepadTimer = new System.Threading.Timer(positionsControl.GamepadTimer_Tick, null, 0, 1000);
                positionsControl.gamepadPollTimer = new System.Threading.Timer(positionsControl.GamepadPollTimer_Tick, null, 0, 1001);
            });
        }

        private void UpdateGameManager(object state)
        {
            for (; ; )
            {
                try
                {
                    if (gameManager == null || formClosing || I_GameHandler == null)
                    {
                        break;
                    }

                    string error = gameManager.Error;
                    if (!string.IsNullOrEmpty(error))
                    {
                        RegistryUtil.RestoreRegistry("Error Restore from MainForm");
                        handler_Ended();
                        return;
                    }

                    I_GameHandler.Update(I_GameHandler.TimerInterval, false);
                    Thread.Sleep(TimeSpan.FromMilliseconds(I_GameHandler.TimerInterval));
                }
                catch (ThreadAbortException)
                {
                    throw;
                }
            }
        }

        private void btn_Prev_Click(object sender, EventArgs e)
        {
            currentStepIndex--;

            if (currentStepIndex < 0)
            {
                currentStepIndex = 0;
                return;
            }

            GameProfile.Ready = false;
            GoToStep(currentStepIndex);
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            SearchGame();
        }

        public void SearchGame(string exeName = null)
        {
            try
            {
                using (System.Windows.Forms.OpenFileDialog open = new System.Windows.Forms.OpenFileDialog())
                {
                    if (string.IsNullOrEmpty(exeName))
                    {
                        open.Title = "Select a game executable to add to Nucleus";
                        open.Filter = "Game Executable Files|*.exe";
                    }
                    else
                    {
                        open.Title = string.Format("Select {0} to add the game to Nucleus", exeName);
                        open.Filter = "Game Exe|" + exeName;
                    }

                    if (open.ShowDialog() == DialogResult.OK)
                    {
                        string path = open.FileName;

                        List<GenericGameInfo> info = gameManager.GetGames(path);

                        if (info.Count > 1)
                        {
                            GameList list = new GameList(info);

                            if (list.ShowDialog() == DialogResult.OK)
                            {
                                UserGameInfo game = GameManager.Instance.TryAddGame(path, list.Selected);

                                if (game != null)
                                {
                                    MessageBox.Show(string.Format("The game {0} has been added!", game.Game.GameName), "Nucleus - Game added");
                                    RefreshGames(false);
                                }
                            }
                        }
                        else if (info.Count == 1)
                        {

                            UserGameInfo game = GameManager.Instance.TryAddGame(path, info[0]);
                            if (gameContextMenuStrip != null)
                                MessageBox.Show(string.Format("The game {0} has been added!", game.Game.GameName), "Nucleus - Game added");
                                RefreshGames(false);
                        }
                        else
                        {
                            MessageBox.Show(string.Format("The executable '{0}' was not found in any game handler's Game.ExecutableName field. Game has not been added.", Path.GetFileName(path)), "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception)
            { }
        }

        private void btnAutoSearch_Click(object sender, EventArgs e)
        {
            if (!searchDisksForm.Visible)
            {
                searchDisksForm.Location = new Point(Width / 2 - searchDisksForm.Width / 2, Height / 2 - searchDisksForm.Height / 2); ;
                searchDisksForm.BringToFront();
                searchDisksForm.Visible = true;
            }
            else
            {
                searchDisksForm.Visible = false;
            }
        }

        private void Form_FormClosed(object sender, FormClosedEventArgs e)
        {
            User32Util.ShowTaskBar();
        }

        private void btnShowTaskbar_Click(object sender, EventArgs e)
        {
            User32Util.ShowTaskBar();
        }

        private void DetailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GetGameDetails.GetDetails(gameManager,currentGameInfo);
        }

        private void DeleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RemoveGame.Remove(this,gameManager, currentGameInfo,false);
        }

        private void GameContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Control selectedControl = FindControlAtCursor(this);

            if (selectedControl.GetType() == typeof(Label) || selectedControl.GetType() == typeof(PictureBox))
            {
                selectedControl = selectedControl.Parent;
            }

            foreach (Control c in selectedControl.Controls)
            {
                if (c is Label)
                {
                    if (c.Text == "No games")
                    {
                        gameContextMenuStrip.Items[0].Text = "No game selected...";
                        for (int i = 1; i < gameContextMenuStrip.Items.Count; i++)
                        {
                            gameContextMenuStrip.Items[i].Visible = false;
                        }
                        return;
                    }
                }
            }

            if (selectedControl.GetType() == typeof(GameControl) || selectedControl.GetType() == typeof(Button))
            {
                bool btnClick = false;
                if (selectedControl.GetType() == typeof(GameControl))
                {
                    currentControl = (GameControl)selectedControl;
                    currentGameInfo = currentControl.UserGameInfo;
                    gameContextMenuStrip.Items[0].Visible = true;
                    gameContextMenuStrip.Items[2].Visible = true;
                }
                else
                {
                    btnClick = true;
                    gameContextMenuStrip.Items[0].Visible = false;
                }

                gameContextMenuStrip.Items[1].Visible = false;
                gameContextMenuStrip.Items[7].Visible = false;
                gameContextMenuStrip.Items[8].Visible = false;
                gameContextMenuStrip.Items[9].Visible = false;
                gameContextMenuStrip.Items[10].Visible = false;
                gameContextMenuStrip.Items[11].Visible = false;
                gameContextMenuStrip.Items[12].Visible = false;
                gameContextMenuStrip.Items[13].Visible = false;
                gameContextMenuStrip.Items[14].Visible = false;
                gameContextMenuStrip.Items[15].Visible = false;
                gameContextMenuStrip.Items[20].Visible = false;

                if (string.IsNullOrEmpty(currentGameInfo.GameGuid) || currentGameInfo == null)
                {
                    gameContextMenuStrip.Items[0].Text = "No game selected...";
                    for (int i = 1; i < gameContextMenuStrip.Items.Count; i++)
                    {
                        gameContextMenuStrip.Items[i].Visible = false;
                    }
                }
                else
                {
                    gameContextMenuStrip.Items[0].Text = currentGameInfo.Game.GameName;

                    bool userConfigPathExists = false;
                    bool userSavePathExists = false;
                    bool docConfigPathExists = false;
                    bool docSavePathExists = false;

                    //bool userConfigPathConverted = false;
                    if (currentGameInfo.Game.UserProfileConfigPath?.Length > 0 && currentGameInfo.Game.UserProfileConfigPath.ToLower().StartsWith(@"documents\"))
                    {
                        currentGameInfo.Game.DocumentsConfigPath = currentGameInfo.Game.UserProfileConfigPath.Substring(10);
                        currentGameInfo.Game.UserProfileConfigPath = null;
                        currentGameInfo.Game.DocumentsConfigPathNoCopy = currentGameInfo.Game.UserProfileConfigPathNoCopy;
                        currentGameInfo.Game.ForceDocumentsConfigCopy = currentGameInfo.Game.ForceUserProfileConfigCopy;
                        //userConfigPathConverted = true;
                    }

                    //bool userSavePathConverted = false;
                    if (currentGameInfo.Game.UserProfileSavePath?.Length > 0 && currentGameInfo.Game.UserProfileSavePath.ToLower().StartsWith(@"documents\"))
                    {
                        currentGameInfo.Game.DocumentsSavePath = currentGameInfo.Game.UserProfileSavePath.Substring(10);
                        currentGameInfo.Game.UserProfileSavePath = null;
                        currentGameInfo.Game.DocumentsSavePathNoCopy = currentGameInfo.Game.UserProfileSavePathNoCopy;
                        currentGameInfo.Game.ForceDocumentsSaveCopy = currentGameInfo.Game.ForceUserProfileSaveCopy;
                        //userSavePathConverted = true;
                    }

                    for (int i = 1; i < gameContextMenuStrip.Items.Count; i++)
                    {
                        gameContextMenuStrip.Items[i].Visible = true;

                        if (string.IsNullOrEmpty(currentGameInfo.Game.UserProfileConfigPath) && string.IsNullOrEmpty(currentGameInfo.Game.UserProfileSavePath) && string.IsNullOrEmpty(currentGameInfo.Game.DocumentsConfigPath) && string.IsNullOrEmpty(currentGameInfo.Game.DocumentsSavePath))
                        {
                            if (i == 7)
                            {
                                gameContextMenuStrip.Items[i].Visible = false;
                            }
                        }
                        else if (i == 1)
                        {
                            profilePaths.Clear();
                            profilePaths.Add(Environment.GetEnvironmentVariable("userprofile"));
                            profilePaths.Add(DocumentsRoot);

                            if (currentGameInfo.Game.UseNucleusEnvironment)
                            {
                                string targetDirectory = $@"{NucleusEnvironmentRoot}\NucleusCoop\";

                                if (Directory.Exists(targetDirectory))
                                {
                                    string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory, "*", SearchOption.TopDirectoryOnly);
                                    foreach (string subdirectory in subdirectoryEntries)
                                    {
                                        profilePaths.Add(subdirectory);
                                        if ($@"{Path.GetDirectoryName(DocumentsRoot)}\NucleusCoop\" == targetDirectory)
                                        {
                                            profilePaths.Add(subdirectory + "\\Documents");
                                        }
                                    }
                                }

                                if ($@"{Path.GetDirectoryName(DocumentsRoot)}\NucleusCoop\" != targetDirectory)
                                {
                                    targetDirectory = $@"{Path.GetDirectoryName(DocumentsRoot)}\NucleusCoop\";
                                    if (Directory.Exists(targetDirectory))
                                    {
                                        string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory, "*", SearchOption.TopDirectoryOnly);
                                        foreach (string subdirectory in subdirectoryEntries)
                                        {
                                            profilePaths.Add(subdirectory + "\\Documents");
                                        }
                                    }
                                }
                            }

                        }

                        if (i == 9)
                        {
                            (gameContextMenuStrip.Items[8] as ToolStripMenuItem).DropDownItems.Clear();
                            (gameContextMenuStrip.Items[9] as ToolStripMenuItem).DropDownItems.Clear();
                            if (currentGameInfo.Game.UserProfileConfigPath?.Length > 0)
                            {
                                if (profilePaths.Count > 0)
                                {
                                    try
                                    {

                                        foreach (string profilePath in profilePaths)
                                        {
                                            string currPath = Path.Combine(profilePath, currentGameInfo.Game.UserProfileConfigPath);
                                            if (Directory.Exists(currPath))
                                            {
                                                if (!userConfigPathExists)
                                                {
                                                    userConfigPathExists = true;
                                                }

                                                string nucPrefix = "";
                                                if (Directory.GetParent(profilePath).Name == "NucleusCoop")
                                                {
                                                    nucPrefix = "Nucleus: ";
                                                }

                                                (gameContextMenuStrip.Items[8] as ToolStripMenuItem).DropDownItems.Add(nucPrefix + Path.GetFileName(profilePath.TrimEnd('\\')), null, new EventHandler(UserProfileOpenSubmenuItem_Click));
                                                (gameContextMenuStrip.Items[9] as ToolStripMenuItem).DropDownItems.Add(nucPrefix + Path.GetFileName(profilePath.TrimEnd('\\')), null, new EventHandler(UserProfileDeleteSubmenuItem_Click));

                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {

                                    }
                                }
                            }

                            if (!userConfigPathExists)
                            {
                                gameContextMenuStrip.Items[8].Visible = false;
                                gameContextMenuStrip.Items[9].Visible = false;
                            }
                        }

                        if (i == 11)
                        {
                            (gameContextMenuStrip.Items[10] as ToolStripMenuItem).DropDownItems.Clear();
                            (gameContextMenuStrip.Items[11] as ToolStripMenuItem).DropDownItems.Clear();
                            if (currentGameInfo.Game.UserProfileSavePath?.Length > 0)
                            {

                                if (profilePaths.Count > 0)
                                {
                                    try
                                    {
                                        foreach (string profilePath in profilePaths)
                                        {
                                            string currPath = Path.Combine(profilePath, currentGameInfo.Game.UserProfileSavePath);
                                            if (Directory.Exists(currPath))
                                            {
                                                if (!userSavePathExists)
                                                {
                                                    userSavePathExists = true;
                                                }

                                                string nucPrefix = "";
                                                if (Directory.GetParent(profilePath).Name == "NucleusCoop")
                                                {
                                                    nucPrefix = "Nucleus: ";
                                                }

                                                (gameContextMenuStrip.Items[10] as ToolStripMenuItem).DropDownItems.Add(nucPrefix + Path.GetFileName(profilePath.TrimEnd('\\')), null, new EventHandler(UserProfileOpenSubmenuItem_Click));
                                                (gameContextMenuStrip.Items[11] as ToolStripMenuItem).DropDownItems.Add(nucPrefix + Path.GetFileName(profilePath.TrimEnd('\\')), null, new EventHandler(UserProfileDeleteSubmenuItem_Click));
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {

                                    }
                                }

                            }

                            if (!userSavePathExists)
                            {
                                gameContextMenuStrip.Items[10].Visible = false;
                                gameContextMenuStrip.Items[11].Visible = false;
                            }
                        }

                        if (i == 13)
                        {
                            (gameContextMenuStrip.Items[12] as ToolStripMenuItem).DropDownItems.Clear();
                            (gameContextMenuStrip.Items[13] as ToolStripMenuItem).DropDownItems.Clear();
                            if (currentGameInfo.Game.DocumentsConfigPath?.Length > 0)
                            {

                                if (profilePaths.Count > 0)
                                {
                                    try
                                    {
                                        foreach (string profilePath in profilePaths)
                                        {
                                            string currPath = Path.Combine(profilePath, currentGameInfo.Game.DocumentsConfigPath);
                                            if (Directory.Exists(currPath))
                                            {
                                                if (!docConfigPathExists)
                                                {
                                                    docConfigPathExists = true;
                                                }

                                                string nucPrefix = "";
                                                if (Directory.GetParent(Directory.GetParent(profilePath).ToString()).Name == "NucleusCoop")
                                                {
                                                    nucPrefix = "Nucleus: ";
                                                }

                                                (gameContextMenuStrip.Items[12] as ToolStripMenuItem).DropDownItems.Add(nucPrefix + Directory.GetParent(profilePath).Name, null, new EventHandler(DocOpenSubmenuItem_Click));
                                                (gameContextMenuStrip.Items[13] as ToolStripMenuItem).DropDownItems.Add(nucPrefix + Directory.GetParent(profilePath).Name, null, new EventHandler(DocDeleteSubmenuItem_Click));
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {

                                    }
                                }
                            }

                            if (!docConfigPathExists)
                            {
                                gameContextMenuStrip.Items[12].Visible = false;
                                gameContextMenuStrip.Items[13].Visible = false;
                            }
                        }

                        if (i == 15)
                        {
                            (gameContextMenuStrip.Items[14] as ToolStripMenuItem).DropDownItems.Clear();
                            (gameContextMenuStrip.Items[15] as ToolStripMenuItem).DropDownItems.Clear();
                            if (currentGameInfo.Game.DocumentsSavePath?.Length > 0)
                            {
                                if (profilePaths.Count > 0)
                                {
                                    try
                                    {
                                        foreach (string profilePath in profilePaths)
                                        {
                                            string currPath = Path.Combine(profilePath, currentGameInfo.Game.DocumentsSavePath);
                                            if (Directory.Exists(currPath))
                                            {
                                                if (!docSavePathExists)
                                                {
                                                    docSavePathExists = true;
                                                }

                                                string nucPrefix = "";
                                                if (Directory.GetParent(Directory.GetParent(profilePath).ToString()).Name == "NucleusCoop")
                                                {
                                                    nucPrefix = "Nucleus: ";
                                                }

                                                (gameContextMenuStrip.Items[14] as ToolStripMenuItem).DropDownItems.Add(nucPrefix + Directory.GetParent(profilePath).Name, null, new EventHandler(DocOpenSubmenuItem_Click));
                                                (gameContextMenuStrip.Items[15] as ToolStripMenuItem).DropDownItems.Add(nucPrefix + Directory.GetParent(profilePath).Name, null, new EventHandler(DocDeleteSubmenuItem_Click));
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {

                                    }

                                }
                            }

                            if (!docSavePathExists)
                            {
                                gameContextMenuStrip.Items[14].Visible = false;
                                gameContextMenuStrip.Items[15].Visible = false;
                            }
                        }

                        if (i == 16 && !userConfigPathExists && !userSavePathExists && !docConfigPathExists && !docSavePathExists)
                        {
                            gameContextMenuStrip.Items[7].Visible = false;
                        }

                        if (i == 1 && currentGameInfo.Game.Description == null)
                        {
                            gameContextMenuStrip.Items[i].Visible = false;
                            if (btnClick)
                            {
                                gameContextMenuStrip.Items[2].Visible = false;
                                i++;
                            }
                        }

                        if (i == 20)
                        {
                            if (currentGameInfo.KeepSymLink)
                            {
                                gameContextMenuStrip.Items[20].Image = ImageCache.GetImage(theme + "locked.png");
                            }
                            else
                            {
                                gameContextMenuStrip.Items[20].Image = ImageCache.GetImage(theme + "unlocked.png");

                            }
                        }

                        if (i == 21)
                        {
                            gameContextMenuStrip.Items[21].Visible = false;
                            gameContextMenuStrip.Items[21].Visible = currentGameInfo.Game.UpdateAvailable;
                            gameContextMenuStrip.Items[21].ForeColor = StripMenuUpdateItemFont;
                            gameContextMenuStrip.Items[21].BackColor = StripMenuUpdateItemBack;
                        }


                    }

                    for (int i = 1; i < gameContextMenuStrip.Items.Count; i++)
                    {
                        if ((gameContextMenuStrip.Items[i] as ToolStripMenuItem) != null)
                        {
                            if ((gameContextMenuStrip.Items[i] as ToolStripMenuItem).DropDownItems.Count > 0)
                            {
                                for (int d = 0; d < (gameContextMenuStrip.Items[i] as ToolStripMenuItem).DropDownItems.Count; d++)
                                {
                                    (gameContextMenuStrip.Items[i] as ToolStripMenuItem).DropDownItems[d].BackColor = MenuStripBackColor;
                                    (gameContextMenuStrip.Items[i] as ToolStripMenuItem).DropDownItems[d].ForeColor = MenuStripFontColor;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                gameContextMenuStrip.Items[0].Text = "No game selected...";

                for (int i = 1; i < gameContextMenuStrip.Items.Count; i++)
                {
                    gameContextMenuStrip.Items[i].Visible = false;
                }
            }
        }

        ///https://stackoverflow.com/questions/9260303/how-to-change-menu-hover-color
        private class MyRenderer : ToolStripProfessionalRenderer
        {
            public MyRenderer() : base(new MyColors()) { }
        }

        private class MyColors : ProfessionalColorTable
        {
            string[] rgb_MouseOverColor = Globals.ThemeIni.IniReadValue("Colors", "Selection").Split(',');
            string[] rgb_MenuStripBackColor = Globals.ThemeIni.IniReadValue("Colors", "MenuStripBack").Split(',');

            public override Color MenuItemSelected => Color.FromArgb(int.Parse(rgb_MouseOverColor[0]), int.Parse(rgb_MouseOverColor[1]), int.Parse(rgb_MouseOverColor[2]), int.Parse(rgb_MouseOverColor[3]));

            public override Color MenuItemBorder => Color.FromArgb(int.Parse(rgb_MouseOverColor[0]), int.Parse(rgb_MouseOverColor[1]), int.Parse(rgb_MouseOverColor[2]), int.Parse(rgb_MouseOverColor[3]));

            public override Color ImageMarginGradientBegin => Color.FromArgb(int.Parse(rgb_MenuStripBackColor[0]), int.Parse(rgb_MenuStripBackColor[1]), int.Parse(rgb_MenuStripBackColor[2]));

            public override Color ImageMarginGradientMiddle => Color.FromArgb(int.Parse(rgb_MenuStripBackColor[0]), int.Parse(rgb_MenuStripBackColor[1]), int.Parse(rgb_MenuStripBackColor[2]));

            public override Color ImageMarginGradientEnd => Color.FromArgb(int.Parse(rgb_MenuStripBackColor[0]), int.Parse(rgb_MenuStripBackColor[1]), int.Parse(rgb_MenuStripBackColor[2]));
        }

        private void gameContextMenuStrip_Opened(object sender, EventArgs e)
        {
            gameContextMenuStrip.Region = Region.FromHrgn(GlobalWindowMethods.CreateRoundRectRgn(2, 2, gameContextMenuStrip.Width - 1, gameContextMenuStrip.Height, 20, 20));
        }

        private void UserProfileOpenSubmenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            ToolStripItem parent = item.OwnerItem;

            string pathSuffix;
            if (parent.Text.Contains("Config"))
            {
                pathSuffix = currentGameInfo.Game.UserProfileConfigPath;
            }
            else
            {
                pathSuffix = currentGameInfo.Game.UserProfileSavePath;
            }

            string path;
            if (item.Text.StartsWith("Nucleus: "))
            {
                path = Path.Combine($@"{NucleusEnvironmentRoot}\NucleusCoop\{item.Text.Substring("Nucleus: ".Length)}\", pathSuffix);
            }
            else
            {
                path = Path.Combine(Environment.GetEnvironmentVariable("userprofile"), pathSuffix);
            }

            if (Directory.Exists(path))
            {
                Process.Start(path);
            }
        }

        private void UserProfileDeleteSubmenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            ToolStripItem parent = item.OwnerItem;

            string pathSuffix;
            if (parent.Text.Contains("Config"))
            {
                pathSuffix = currentGameInfo.Game.UserProfileConfigPath;
            }
            else
            {
                pathSuffix = currentGameInfo.Game.UserProfileSavePath;
            }

            string path;
            if (item.Text.StartsWith("Nucleus: "))
            {
                path = Path.Combine($@"{NucleusEnvironmentRoot}\NucleusCoop\{item.Text.Substring("Nucleus: ".Length)}\", pathSuffix);
            }
            else
            {
                path = Path.Combine(Environment.GetEnvironmentVariable("userprofile"), pathSuffix);
            }

            if (Directory.Exists(path))
            {
                DialogResult dialogResult = MessageBox.Show("Are you sure you want to delete '" + path + "' and all its contents?", "Confirm deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dialogResult == DialogResult.Yes)
                {
                    Directory.Delete(path, true);
                }
            }
        }

        private void DocOpenSubmenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            ToolStripItem parent = item.OwnerItem;

            string pathSuffix;
            if (parent.Text.Contains("Config"))
            {
                pathSuffix = currentGameInfo.Game.DocumentsConfigPath;
            }
            else
            {
                pathSuffix = currentGameInfo.Game.DocumentsSavePath;
            }

            string path;
            if (item.Text.StartsWith("Nucleus: "))
            {
                path = Path.Combine($@"{Path.GetDirectoryName(DocumentsRoot)}\NucleusCoop\{item.Text.Substring("Nucleus: ".Length)}\Documents", pathSuffix);
            }
            else
            {
                path = Path.Combine(DocumentsRoot, pathSuffix);
            }

            if (Directory.Exists(path))
            {
                Process.Start(path);
            }
        }

        private void DocDeleteSubmenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            ToolStripItem parent = item.OwnerItem;

            string pathSuffix;
            if (parent.Text.Contains("Config"))
            {
                pathSuffix = currentGameInfo.Game.DocumentsConfigPath;
            }
            else
            {
                pathSuffix = currentGameInfo.Game.DocumentsSavePath;
            }

            string path;
            if (item.Text.StartsWith("Nucleus: "))
            {
                path = Path.Combine($@"{Path.GetDirectoryName(DocumentsRoot)}\NucleusCoop\{item.Text.Substring("Nucleus: ".Length)}\Documents", pathSuffix);
            }
            else
            {
                path = Path.Combine(DocumentsRoot, pathSuffix);
            }

            if (Directory.Exists(path))
            {
                DialogResult dialogResult = MessageBox.Show("Are you sure you want to delete '" + path + "' and all its contents?", "Confirm deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dialogResult == DialogResult.Yes)
                {
                    Directory.Delete(path, true);
                }
            }
        }

        public static Control FindControlAtPoint(Control container, Point pos)
        {
            Control child;
            foreach (Control c in container.Controls)
            {
                if (c.Visible && c.Bounds.Contains(pos))
                {
                    child = FindControlAtPoint(c, new Point(pos.X - c.Left, pos.Y - c.Top));
                    if (child == null)
                    {
                        return c;
                    }
                    else
                    {
                        return child;
                    }
                }
            }

            return null;
        }

        public static Control FindControlAtCursor(MainForm form)
        {
            Point pos = Cursor.Position;
            if (form.Bounds.Contains(pos))
            {
                return FindControlAtPoint(form, form.PointToClient(pos));
            }

            return null;
        }

        private void OpenScriptToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenHandler.OpenRawHandler(gameManager,currentGameInfo);
        }

        private void OpenDataFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenGameContentFolder.OpenDataFolder(gameManager,currentGameInfo);
        }

        private void ChangeIconToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (System.Windows.Forms.OpenFileDialog dlg = new System.Windows.Forms.OpenFileDialog())
            {
                dlg.Title = "Open Image";
                dlg.Filter = "All Images Files (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tiff;*.tif;*.ico;*.exe)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tiff;*.tif;*.ico;*.exe" +
                            "|PNG Portable Network Graphics (*.png)|*.png" +
                            "|JPEG File Interchange Format (*.jpg *.jpeg *jfif)|*.jpg;*.jpeg;*.jfif" +
                            "|BMP Windows Bitmap (*.bmp)|*.bmp" +
                            "|TIF Tagged Imaged File Format (*.tif *.tiff)|*.tif;*.tiff" +
                            "|Icon (*.ico)|*.ico" +
                            "|Executable (*.exe)|*.exe";
                dlg.InitialDirectory = Path.Combine(Directory.GetCurrentDirectory() + "\\gui\\icons");

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    // Create a ImageCache.GetImage object from the picture file on disk,
                    // and assign that to the PictureBox.Image property
                    //PictureBox1.Image = ImageCache.GetImage(dlg.FileName);

                    if (dlg.FileName.EndsWith(".exe"))
                    {
                        Icon icon = Shell32.GetIcon(dlg.FileName, false);

                        Bitmap bmp = icon.ToBitmap();
                        icon.Dispose();
                        currentGameInfo.Icon = bmp;
                    }
                    else
                    {
                        currentGameInfo.Icon = ImageCache.GetImage(dlg.FileName);
                    }

                    iconsIni.IniWriteValue("GameIcons", currentGameInfo.Game.GameName, dlg.FileName);

                    GetIcon(currentGameInfo);
                    RefreshGames(false);
                }
            }
        }

        private void Log(string logMessage)
        {
            if (ini.IniReadValue("Misc", "DebugLog") == "True")
            {
                using (StreamWriter writer = new StreamWriter("debug-log.txt", true))
                {
                    writer.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}]MAIN: {logMessage}");
                    writer.Close();
                }
            }
        }

        private void ScriptNotesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(currentGameInfo.Game.Description, "Handler Author's Notes", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void GameOptions_Click(object sender, EventArgs e)
        {
            Button btnSender = (Button)sender;
            Point ptLowerLeft = new Point(0, btnSender.Height);
            ptLowerLeft = btnSender.PointToScreen(ptLowerLeft);
            gameContextMenuStrip.Show(ptLowerLeft);
        }

        private void OpenOrigExePathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = Path.GetDirectoryName(currentGameInfo.ExePath);
            if (Directory.Exists(path))
            {
                Process.Start(path);
            }
            else
            {
                MessageBox.Show("Unable to open original executable path for this game.", "Not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void deleteContentFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = Path.Combine(gameManager.GetAppContentPath(), currentGameInfo.Game.GUID);
            if (Directory.Exists(path))
            {
                DialogResult dialogResult = MessageBox.Show("Are you sure you want to delete '" + path + "' and all its contents?", "Confirm deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dialogResult == DialogResult.Yes)
                {
                    Directory.Delete(path, true);
                }
            }
            else
            {
                MessageBox.Show("No data in content folder to delete.");
            }
        }

        private void btn_Download_Click(object sender, EventArgs e)
        {
            scriptDownloader.ShowDialog();
        }

        private void button_UpdateAvailable_Click(object sender, EventArgs e)
        {
            handler = scriptDownloader.GetHandler(currentGameInfo.Game.GetHandlerId());

            if (handler == null)
            {
                button_UpdateAvailable.Visible = false;
                MessageBox.Show("Error fetching update information", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                DialogResult dialogResult = MessageBox.Show("An update to this handler is available, download it?", "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dialogResult == DialogResult.Yes)
                {
                    RemoveGame.Remove(this, gameManager, currentGameInfo, true);
                    StepPanel.Visible = false;
                    label_StepTitle.Text = "Select a game";
                    btn_Play.Enabled = false;
                    btn_Next.Enabled = false;
                    button_UpdateAvailable.Visible = false;
                    stepsList.Clear();

                    downloadPrompt = new DownloadPrompt(handler, this, null, true);
                    list_Games.SuspendLayout();
                    downloadPrompt.ShowDialog();
                    list_Games.ResumeLayout();
                    StepPanel.Visible = true;
                }
            }
        }

        private void updateHandlerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            handler = scriptDownloader.GetHandler(currentGameInfo.Game.GetHandlerId());

            if (handler == null)
            {
                MessageBox.Show("Error fetching update information", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                btn_Play.Enabled = false;
                btn_Next.Enabled = false;

                downloadPrompt = new DownloadPrompt(handler, this, null, true);
                downloadPrompt.gameExeNoUpdate = true;
                list_Games.SuspendLayout();
                downloadPrompt.ShowDialog();
                list_Games.ResumeLayout();

                if (currentGameSetup == currentGameInfo.Game.GameName)
                {
                    button_UpdateAvailable.Visible = false;
                }

                for (int i = 0; i < gameManager.User.Games.Count; i++)
                {
                    if (gameManager.User.Games[i].Game != null)
                    {
                        if (gameManager.User.Games[i].Game.GameName == currentGameInfo.Game.GameName)
                        {

                            string path = gameManager.User.Games[i].ExePath;
                            gameManager.User.Games.RemoveAt(i);
                            List<GenericGameInfo> info = gameManager.GetGames(path);

                            if (info.Count == 1)
                            {
                                UserGameInfo game = GameManager.Instance.TryAddGame(path, info[0]);
                                break;
                            }
                        }
                    }
                }

                if (StepPanel.Visible)
                {
                    rightFrame.Visible = false;
                    StepPanel.Visible = false;
                    clientAreaPanel.BackgroundImage = defBackground;
                    stepPanelPictureBox.Visible = true;
                }

                RefreshGames(false);
            }
        }

        private void btn_Extract_Click(object sender, EventArgs e)
        {
            ExtractHandler.Extract(this);
        }

        private void btn_SplitCalculator_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start((Path.Combine(Application.StartupPath, @"utils\SplitCalculator\SplitCalculator.exe")));
            }
            catch (Exception)
            {
                MessageBox.Show(@"SplitCalculator.exe has not been found in the utils\SplitCalculator folder. Try again with a fresh Nucleus Co-op installation.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btn_noHub_Click(object sender, EventArgs e)
        {
            Connected = StartChecks.CheckNetCon();
        }

        private void btn_Links_Click(object sender, EventArgs e)
        {
            if (linksPanel.Visible)
            {
                linksPanel.Visible = false;
                btn_Links.BackgroundImage = ImageCache.GetImage(theme + "title_dropdown_closed.png");

                if (third_party_tools_container.Visible)
                {
                    third_party_tools_container.Visible = false;
                }
            }
            else
            {
                linksPanel.BringToFront();
                linksPanel.Visible = true;
                btn_Links.BackgroundImage = ImageCache.GetImage(theme + "title_dropdown_opened.png");
            }
        }

        private void this_Click(object sender, System.EventArgs e)
        {
            linksPanel.Visible = false;
            btn_Links.BackgroundImage = ImageCache.GetImage(theme + "title_dropdown_closed.png");

            if (third_party_tools_container.Visible)
            {
                third_party_tools_container.Visible = false;
            }
        }

        private void button1_Click_2(object sender, EventArgs e) { Process.Start("https://hub.splitscreen.me/"); }

        private void button1_Click(object sender, EventArgs e) { Process.Start("https://discord.com/invite/QDUt8HpCvr"); }

        private void button2_Click(object sender, EventArgs e) { Process.Start("https://www.reddit.com/r/nucleuscoop/"); }

        private void logo_Click(object sender, EventArgs e) { Process.Start("https://github.com/SplitScreen-Me/splitscreenme-nucleus/releases"); }

        private void link_faq_Click(object sender, EventArgs e) { Process.Start(faq_link); }

        private void linkLabel4_LinkClicked(object sender, EventArgs e) { Process.Start("https://github.com/nefarius/ScpToolkit/releases"); third_party_tools_container.Visible = false; }

        private void linkLabel3_LinkClicked(object sender, EventArgs e) { Process.Start("https://github.com/ViGEm/HidHide/releases"); third_party_tools_container.Visible = false; }

        private void linkLabel2_LinkClicked(object sender, EventArgs e) { Process.Start("https://github.com/Ryochan7/DS4Windows/releases"); third_party_tools_container.Visible = false; }

        private void linkLabel1_LinkClicked(object sender, EventArgs e) { Process.Start("https://github.com/csutorasa/XOutput/releases"); third_party_tools_container.Visible = false; }

        private void btn_thirdPartytools_Click(object sender, EventArgs e) { if (third_party_tools_container.Visible) { third_party_tools_container.Visible = false; } else { third_party_tools_container.Visible = true; } }

        private void scriptAuthorTxt_LinkClicked(object sender, LinkClickedEventArgs e) { Process.Start(e.LinkText); }

        private void closeBtn_MouseEnter(object sender, EventArgs e) { closeBtn.BackgroundImage = ImageCache.GetImage(theme + "title_close_mousehover.png"); }

        private void closeBtn_MouseLeave(object sender, EventArgs e) { closeBtn.BackgroundImage = ImageCache.GetImage(theme + "title_close.png"); }

        private void maximizeBtn_MouseEnter(object sender, EventArgs e) { maximizeBtn.BackgroundImage = ImageCache.GetImage(theme + "title_maximize_mousehover.png"); }

        private void maximizeBtn_MouseLeave(object sender, EventArgs e) { maximizeBtn.BackgroundImage = ImageCache.GetImage(theme + "title_maximize.png"); }

        private void minimizeBtn_MouseLeave(object sender, EventArgs e) { minimizeBtn.BackgroundImage = ImageCache.GetImage(theme + "title_minimize.png"); }

        private void minimizeBtn_MouseEnter(object sender, EventArgs e) { minimizeBtn.BackgroundImage = ImageCache.GetImage(theme + "title_minimize_mousehover.png"); }

        private void btn_settings_MouseEnter(object sender, EventArgs e) { if (profileSettings.Visible) { return; } btn_settings.BackgroundImage = ImageCache.GetImage(theme + "title_settings_mousehover.png"); }

        private void btn_settings_MouseLeave(object sender, EventArgs e) { btn_settings.BackgroundImage = ImageCache.GetImage(theme + "title_settings.png"); }

        private void btn_downloadAssets_MouseEnter(object sender, EventArgs e) { btn_downloadAssets.BackgroundImage = ImageCache.GetImage(theme + "title_download_assets_mousehover.png"); }

        private void btn_downloadAssets_MouseLeave(object sender, EventArgs e) { btn_downloadAssets.BackgroundImage = ImageCache.GetImage(theme + "title_download_assets.png"); }

        private void btn_faq_MouseEnter(object sender, EventArgs e) { btn_faq.BackgroundImage = ImageCache.GetImage(theme + "faq_mousehover.png"); }

        private void btn_faq_MouseLeave(object sender, EventArgs e) { btn_faq.BackgroundImage = ImageCache.GetImage(theme + "faq.png"); }

        private void btn_reddit_MouseEnter(object sender, EventArgs e) { btn_reddit.BackgroundImage = ImageCache.GetImage(theme + "reddit_mousehover.png"); }

        private void btn_reddit_MouseLeave(object sender, EventArgs e) { btn_reddit.BackgroundImage = ImageCache.GetImage(theme + "reddit.png"); }

        private void btn_Discord_MouseEnter(object sender, EventArgs e) { btn_Discord.BackgroundImage = ImageCache.GetImage(theme + "discord_mousehover.png"); }

        private void btn_Discord_MouseLeave(object sender, EventArgs e) { btn_Discord.BackgroundImage = ImageCache.GetImage(theme + "discord.png"); }

        private void btn_SplitCalculator_MouseEnter(object sender, EventArgs e) { btn_SplitCalculator.BackgroundImage = ImageCache.GetImage(theme + "splitcalculator_mousehover.png"); }

        private void btn_SplitCalculator_MouseLeave(object sender, EventArgs e) { btn_SplitCalculator.BackgroundImage = ImageCache.GetImage(theme + "splitcalculator.png"); }

        private void btn_thirdPartytools_MouseEnter(object sender, EventArgs e) { btn_thirdPartytools.BackgroundImage = ImageCache.GetImage(theme + "thirdPartytools_mousehover.png"); }

        private void btn_thirdPartytools_MouseLeave(object sender, EventArgs e) { btn_thirdPartytools.BackgroundImage = ImageCache.GetImage(theme + "thirdPartytools.png"); }

        private void btn_magnifier_Click(object sender, EventArgs e)
        {
            if (!positionsControl.textZoomContainer.Visible)
            {
                positionsControl.textZoomContainer.Region = Region.FromHrgn(GlobalWindowMethods.CreateRoundRectRgn(0, 0, positionsControl.textZoomContainer.Width, positionsControl.textZoomContainer.Height, 15, 15));
                positionsControl.handlerNoteZoom.Text = scriptAuthorTxt.Text;
                positionsControl.handlerNoteZoom.Visible = true;
                positionsControl.textZoomContainer.Visible = true;
                btn_magnifier.Image = ImageCache.GetImage(theme + "magnifier_close.png");
            }
            else
            {
                positionsControl.textZoomContainer.Visible = false;
                btn_magnifier.Image = ImageCache.GetImage(theme + "magnifier.png");
            }
        }

        private void btn_textSwitcher_Click(object sender, EventArgs e)
        {
            if (positionsControl.textZoomContainer.Visible)
            {
                return;
            }

            bool gameDesExist = !positionsControl.textZoomContainer.Visible && File.Exists(Path.Combine(Application.StartupPath, $@"gui\descriptions\" + currentGame.GUID + ".txt"));
            bool notesExist = currentGame.Description != null;

            if (gameDesExist && !HandlerNoteTitle.Text.Contains("Profile n°"))
            {
                StreamReader desc = new StreamReader(Path.Combine(Application.StartupPath, $@"gui\descriptions\" + currentGame.GUID + ".txt"));
                if (HandlerNoteTitle.Text == "Handler Notes" || HandlerNoteTitle.Text == "Read First")
                {
                    HandlerNoteTitle.Text = "Game Description";
                    scriptAuthorTxt.Text = desc.ReadToEnd();
                    desc.Dispose();
                }
                else if (notesExist)
                {
                    HandlerNoteTitle.Text = "Handler Notes";
                    scriptAuthorTxt.Text = currentGame.Description;
                    desc.Dispose();
                }
            }
            else if (HandlerNoteTitle.Text.Contains("Profile n°"))
            {

                if (gameDesExist)
                {
                    StreamReader desc = new StreamReader(Path.Combine(Application.StartupPath, $@"gui\descriptions\" + currentGame.GUID + ".txt"));

                    HandlerNoteTitle.Text = "Game Description";
                    scriptAuthorTxt.Text = desc.ReadToEnd();
                    desc.Dispose();
                }
                else if (notesExist)
                {
                    HandlerNoteTitle.Text = "Handler Notes";
                    scriptAuthorTxt.Text = currentGame.Description;
                }
            }

            btn_textSwitcher.Visible = (gameDesExist && notesExist);
        }

        private int clickCount = 0;

        private void stepPanelPictureBox_Click(object sender, EventArgs e)
        {
            clickCount++;

            if (clickCount < 3)
            {
                return;
            }

            if (connected)
                TriggerHubShowCase();
        }

        public void button_Click(object sender, EventArgs e)
        {
            if (mouseClick)
            {
                SoundPlayer(theme + "button_click.wav");
            }
        }

        private void SettingsBtn_Click(object sender, EventArgs e)
        {
            if (profileSettings.Visible)
            {
                return;
            }

            if (!settings.Visible)
            {
                settings.Location = new Point(Width / 2 - settings.Width / 2, Height / 2 - settings.Height / 2);
                settings.BringToFront();
                settings.Visible = true;
            }
            else
            {
                settings.Visible = false;
            }
        }

        private void gameProfilesList_btn_Click(object sender, EventArgs e)
        {
            if (settings.Visible)
            {
                return;
            }

            if (GameProfile.profilesPathList.Count == 0)
            {
                positionsControl.gameProfilesList.Visible = false;
                positionsControl.gameProfilesList_btn.Image = ImageCache.GetImage(theme + "profiles_list.png");
                return;
            }

            if (positionsControl.gameProfilesList.Visible)
            {
                positionsControl.gameProfilesList.Visible = false;
                positionsControl.gameProfilesList_btn.Image = ImageCache.GetImage(theme + "profiles_list.png");
            }
            else
            {
                positionsControl.gameProfilesList.Visible = true;
                positionsControl.gameProfilesList_btn.Image = ImageCache.GetImage(theme + "profiles_list_opened.png");
            }
        }

        public void ProfileSettings_btn_Click(object sender, EventArgs e)
        {
            if (settings.Visible)
            {
                return;
            }

            if (!profileSettings.Visible)
            {
                profileSettings.Location = new Point(Width / 2 - profileSettings.Width / 2, Height / 2 - profileSettings.Height / 2);
                profileSettings.BringToFront();
                profileSettings.Visible = true;
                ProfilesList.profilesList.Locked = true;
                ProfileSettings.UpdateProfileSettingsUiValues();
            }
        }

        private void minimizeButton(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void maximizeButton(object sender, EventArgs e)
        {
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        }

        private void MainForm_ClientSizeChanged(object sender, EventArgs e)
        {
            Invalidate();

            if (roundedcorners)
            {
                if (WindowState == FormWindowState.Maximized)
                {
                    Region = Region.FromHrgn(GlobalWindowMethods.CreateRoundRectRgn(0, 0, Width, Height, 0, 0));
                    clientAreaPanel.Region = Region.FromHrgn(GlobalWindowMethods.CreateRoundRectRgn(0, 0, clientAreaPanel.Width, clientAreaPanel.Height, 0, 0));
                    glowingLine0.Region = Region.FromHrgn(GlobalWindowMethods.CreateRoundRectRgn(0, 0, clientAreaPanel.Width, clientAreaPanel.Height, 0, 0));
                }
                else
                {
                    Region = Region.FromHrgn(GlobalWindowMethods.CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
                    clientAreaPanel.Region = Region.FromHrgn(GlobalWindowMethods.CreateRoundRectRgn(0, 0, clientAreaPanel.Width, clientAreaPanel.Height, 20, 20));
                    glowingLine0.Region = Region.FromHrgn(GlobalWindowMethods.CreateRoundRectRgn(0, 0, clientAreaPanel.Width, clientAreaPanel.Height, 20, 20));
                }
            }

            if (profileSettings != null) profileSettings.Visible = false;
            if (settings != null) settings.Visible = false;
            if (searchDisksForm != null) searchDisksForm.Visible = false;

            if (positionsControl != null)
            {
                positionsControl.textZoomContainer.Visible = false;
                btn_magnifier.Image = ImageCache.GetImage(theme + "magnifier.png");
            }
            if (ProfilesList.profilesList != null)
            {
                ProfilesList.profilesList.Locked = false;
            }
        }

        private void MainForm_ResizeBegin(object sender, EventArgs e)
        {
            foreach (Control titleBarButtons in Controls)
            {
                titleBarButtons.Visible = false;
            }

            btn_Links.BackgroundImage = ImageCache.GetImage(theme + "title_dropdown_closed.png");
            clientAreaPanel.Visible = false;
            Opacity = 0.6D;
        }

        private void MainForm_ResizeEnd(object sender, EventArgs e)
        {
            foreach (Control titleBarButtons in Controls)
            {
                titleBarButtons.Visible = titleBarButtons.Name != "third_party_tools_container" && titleBarButtons.Name != "linksPanel";
            }

            if (connected) { btn_noHub.Visible = false; }

            clientAreaPanel.Visible = true;

            Opacity = 1.0D;
        }

        private void closeButton(object sender, EventArgs e)
        {
            SaveNucleusWindowPosAndLoc();

            Process[] processes = Process.GetProcessesByName("SplitCalculator");
            foreach (Process SplitCalculator in processes)
            {
                SplitCalculator.Kill();
            }

            Process.GetCurrentProcess().Kill();
        }

        private void keepInstancesFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (currentGameInfo.KeepSymLink)
            {
                currentGameInfo.KeepSymLink = false;
                gameContextMenuStrip.Items[20].Image = ImageCache.GetImage(theme + "locked.png");
            }
            else
            {
                currentGameInfo.KeepSymLink = true;
                gameContextMenuStrip.Items[20].Image = ImageCache.GetImage(theme + "unlocked.png");
            }

            GameManager.Instance.SaveUserProfile();
        }

        private void SaveNucleusWindowPosAndLoc()
        {
            ini.IniWriteValue("Misc", "WindowSize", Width + "X" + Height);
            //var loc = PointToScreen(Location);
            //ini.IniWriteValue("Misc", "WindowLocation",loc.X + "X" + loc.Y);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveNucleusWindowPosAndLoc();
        }
    }
}
