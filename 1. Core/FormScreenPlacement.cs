using System;
using System.Drawing;
using System.Windows.Forms;

namespace MvcVisionSystem
{
    public static class FormScreenPlacement
    {
        public static Screen GetPreferredStartupScreen()
        {
            Screen[] screens = Screen.AllScreens;
            if (screens == null || screens.Length == 0)
            {
                return Screen.PrimaryScreen;
            }

            Screen preferredScreen = screens[0];
            foreach (Screen screen in screens)
            {
                if (screen.Bounds.Left < preferredScreen.Bounds.Left)
                {
                    preferredScreen = screen;
                    continue;
                }

                if (screen.Bounds.Left == preferredScreen.Bounds.Left
                    && GetArea(screen.Bounds) < GetArea(preferredScreen.Bounds))
                {
                    preferredScreen = screen;
                }
            }

            return preferredScreen;
        }

        public static void CenterOnPreferredScreen(Form form)
        {
            if (form == null) return;

            Rectangle workingArea = GetPreferredStartupScreen().WorkingArea;
            int left = workingArea.Left + Math.Max(0, (workingArea.Width - form.Width) / 2);
            int top = workingArea.Top + Math.Max(0, (workingArea.Height - form.Height) / 2);

            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(left, top);
        }

        public static void MaximizeOnPreferredScreen(Form form)
        {
            if (form == null) return;

            Rectangle workingArea = GetPreferredStartupScreen().WorkingArea;
            form.StartPosition = FormStartPosition.Manual;
            form.WindowState = FormWindowState.Normal;
            form.Bounds = workingArea;
            form.WindowState = FormWindowState.Maximized;
        }

        private static int GetArea(Rectangle bounds)
        {
            return bounds.Width * bounds.Height;
        }
    }
}
