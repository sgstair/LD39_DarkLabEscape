using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LD39_sgstair
{
    class NarrationSystem
    {
        public double NarrationAnimateInTime = 0.3;
        public double NarrationAnimateTextTime = 4;
        public double NarrationAnimateOutTime = 0.3;
        public double NarrationDisplayTime = 2;

        public double SystemTextDisplayTime = 15;

        public void Reset()
        {
            NarrationQueue.Clear();
            CurrentEvent = null;
            SystemTextMessage = null;

            LevelExit = false;
            ExitWhenDone = false;
            GameInteractive = false;
        }


        public void Update(double time)
        {
            if (CurrentEvent != null)
            {
                GameInteractive = false;
                EventTime += time;
                double et = EventTime;

                TextDisplayPercent = 0;
                if(et < NarrationAnimateInTime)
                {
                    BoxVisiblePercent = et / NarrationAnimateInTime;
                }
                else
                {
                    BoxVisiblePercent = 1;

                    et -= NarrationAnimateInTime;
                    if(et <= NarrationAnimateTextTime)
                    {
                        TextDisplayPercent = et / NarrationAnimateTextTime;
                    }
                    else
                    {
                        TextDisplayPercent = 1;
                        et -= NarrationAnimateTextTime;
                        if(et > NarrationDisplayTime)
                        {
                            et -= NarrationDisplayTime;
                            if(et < NarrationAnimateOutTime)
                            {
                                BoxVisiblePercent = 1 - et / NarrationAnimateOutTime;
                            }
                            else
                            {
                                StartNextItem();
                            }
                        }
                    }
                }

            }
            else
            {
                if (ExitWhenDone)
                {
                    LevelExit = true;
                }
                else
                {
                    GameInteractive = true;
                }
            }

            if (SystemTextMessage != null)
            {
                SystemTextTime += time;
                if (SystemTextTime > SystemTextDisplayTime) SystemTextMessage = null;
            }
        }

        public void QueuePreLevelContent(int level)
        {
            if (level >= 0 && level < LevelData.Length)
            {
                foreach (NarrationEvent e in LevelData[level].PreLevelEvents)
                {
                    NarrationQueue.Enqueue(e);
                }
                StartNextItem();
            }
        }
        public void QueuePostLevelContent(int level)
        {
            if (level >= 0 && level < LevelData.Length)
            {
                foreach (NarrationEvent e in LevelData[level].PostLevelEvents)
                {
                    NarrationQueue.Enqueue(e);
                }
                ExitWhenDone = true;
                StartNextItem();
            }
        }

        void StartNextItem()
        {
            if(CurrentEvent != null)
            {
                // Execute completion action
                CurrentEvent.CompletionEvent?.Invoke();
            }

            CurrentEvent = null;
            EventTime = 0;
            BoxVisiblePercent = 0;
            TextDisplayPercent = 0;
            GameInteractive = false;

            if(NarrationQueue.Count > 0)
            {
                CurrentEvent = NarrationQueue.Dequeue();
            }
        }


        public void DisplaySystemText(string text)
        {
            SystemTextMessage = text;
            SystemTextTime = 0;
        }

        public Queue<NarrationEvent> NarrationQueue = new Queue<NarrationEvent>();

        public NarrationEvent CurrentEvent;
        public double EventTime;
        public double BoxVisiblePercent;
        public double TextDisplayPercent;

        public bool GameInteractive;
        public bool ExitWhenDone = false;
        public bool LevelExit = false;

        public string SystemTextMessage;
        public double SystemTextTime;


        static void EvtPowerDown()
        {
            GameAutomation.State.ScriptedOff = true;
        }
        static void EvtPowerBackup()
        {
            GameAutomation.State.ScriptedOff = false;
            GameAutomation.State.DrainPaused = false;
            GameAutomation.Narration.DisplaySystemText("Backup Power Online");
        }
        static void EvtPowerGeneratorFailure()
        {
            GameAutomation.Narration.DisplaySystemText("Main Generator Failure - Please Evacuate");
        }

            // Each level has a narrationcontext
        NarrationContext[] LevelData = new NarrationContext[]
        {
            new NarrationContext()
            {
                PreLevelEvents = new NarrationEvent[]
                {
                    new NarrationEvent() { Text = "Now just to download the proposed schematics for this new energy beam lab...", CompletionEvent = EvtPowerDown },
                    new NarrationEvent() { Text = "WHAT?! A power outage? I'm never going to get my lab set up in time!", CompletionEvent = EvtPowerBackup },
                    new NarrationEvent() { Text = "Well just great. I hope the power comes back soon.", CompletionEvent = EvtPowerGeneratorFailure },
                    new NarrationEvent() { Text = "Idiots, the lot of them!!! And of course the security doors won't open. Whoever wired this place was utterly incompetent." },
                    new NarrationEvent() { Text = "Hmm, I heard a rumor that the energy beam can activate the security door." },
                    new NarrationEvent() { Text = "Maybe I should give that a try..." },
                },
                PostLevelEvents = new NarrationEvent[]
                {
                    new NarrationEvent() { Text = "So it works! This facility no longer seems very secure at all." },
                }
            },
           new NarrationContext()
            {
                PreLevelEvents = new NarrationEvent[]
                {
                },
                PostLevelEvents = new NarrationEvent[]
                {
                }
            },
            new NarrationContext()
            {
                PreLevelEvents = new NarrationEvent[]
                {
                },
                PostLevelEvents = new NarrationEvent[]
                {
                }
            },
             new NarrationContext()
            {
                PreLevelEvents = new NarrationEvent[]
                {
                },
                PostLevelEvents = new NarrationEvent[]
                {
                }
            },
            new NarrationContext()
            {
                PreLevelEvents = new NarrationEvent[]
                {
                },
                PostLevelEvents = new NarrationEvent[]
                {
                }
            },
            new NarrationContext()
            {
                PreLevelEvents = new NarrationEvent[]
                {
                },
                PostLevelEvents = new NarrationEvent[]
                {
                }
            },
        };
    }

    class NarrationContext
    {
        public NarrationEvent[] PreLevelEvents;
        public NarrationEvent[] PostLevelEvents;
    }

    class NarrationEvent
    {
        public int ContextValue;
        public string Text;
        public Action CompletionEvent;
    }
}
