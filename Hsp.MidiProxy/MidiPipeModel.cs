﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Shapes;
using Eos.Mvvm;
using Eos.Mvvm.Commands;
using Hsp.Midi;
using Hsp.Midi.Messages;

namespace Hsp.MidiProxy;

public class MidiPipeModel : ViewModelBase
{
  public string RequestedInputDeviceName { get; }
  public string RequesteOutputDeviceName { get; }

  private MidiPipe _pipe;

  public bool IsOpen
  {
    get => GetAutoFieldValue<bool>();
    private set
    {
      SetAutoFieldValue(value);
      Dispatch(() =>
      {
        OpenCommand.RaiseCanExecuteChanged();
        CloseCommand.RaiseCanExecuteChanged();
        TestCommand.RaiseCanExecuteChanged();
      });
    }
  }


  public MidiDeviceList InputDevices { get; } = new(true);

  public MidiDeviceList OutputDevices { get; } = new(false);


  public UiCommand OpenCommand => GetAutoFieldValue(() => new UiCommand
  {
    ExecuteFunction = async _ => Connect(),
    CanExecuteCallback = _ => !IsOpen
  });

  public UiCommand TestCommand => GetAutoFieldValue(() => new UiCommand
  {
    ExecuteFunction = async _ => Test(),
    CanExecuteCallback = _ => IsOpen
  });

  public UiCommand CloseCommand => GetAutoFieldValue(() => new UiCommand
  {
    ExecuteFunction = async _ => Disconnect(),
    CanExecuteCallback = _ => IsOpen
  });


  public event EventHandler<IMidiMessage> MessageReceived;


  public MidiPipeModel()
    : this(null, null)
  {
  }

  public MidiPipeModel(string requestedInputDeviceName, string requesteOutputDeviceName)
  {
    RequestedInputDeviceName = requestedInputDeviceName;
    RequesteOutputDeviceName = requesteOutputDeviceName;

    Task.Run(async () =>
    {
      await InputDevices.Refresh();
      await OutputDevices.Refresh();
      InputDevices.SelectedItem = InputDevices.FirstOrDefault(d => d.Name == RequestedInputDeviceName);
      OutputDevices.SelectedItem = OutputDevices.FirstOrDefault(d => d.Name == RequesteOutputDeviceName);
    });
  }


  private void PipeOnMessageReceived(object sender, IMidiMessage e)
  {
    MessageReceived?.Invoke(this, e);
    WriteMidiLog(e);
  }

  private void WriteMidiLog(IMidiMessage midiMessage)
  {
    if (midiMessage is ChannelMessage cm && (cm.Channel > 0 || cm.Command == ChannelCommand.Controller)) return;
    var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "MidiLog.txt");
    var line = new[]
    {
      DateTime.Now.ToLongTimeString(),
      midiMessage.ToString()
    };
    File.AppendAllText(path, string.Join("\t", line) + Environment.NewLine);
  }

  public void Test()
  {
    ToggleRecord(true);
  }

  private void ToggleRecord(bool overdub)
  {
    if (overdub)
      SetShift(true);
    _pipe.OutputMidiDevice.Send(new ChannelMessage(ChannelCommand.NoteOn, 0, 92, 127));
    _pipe.OutputMidiDevice.Send(new ChannelMessage(ChannelCommand.NoteOff, 0, 92));
    if (overdub)
      SetShift(false);
  }

  private void SetShift(bool b)
  {
    if (b)
      _pipe.OutputMidiDevice.Send(new ChannelMessage(ChannelCommand.NoteOn, 0, 120, 127));
    else
    {
      _pipe.OutputMidiDevice.Send(new ChannelMessage(ChannelCommand.NoteOff, 0, 120));
      _pipe.OutputMidiDevice.Send(new ChannelMessage(ChannelCommand.NoteOn, 0, 75, 1));
    }
  }

  public void Connect()
  {
    if (IsOpen) return;
    if (InputDevices.SelectedItem == null || OutputDevices.SelectedItem == null) return;
    _pipe = new MidiPipe(InputDevices.SelectedItem.Name, OutputDevices.SelectedItem.Name);
    _pipe.MessageReceived += PipeOnMessageReceived;
    _pipe.Open();
    IsOpen = true;
  }

  public void Disconnect()
  {
    if (!IsOpen) return;
    _pipe.MessageReceived -= PipeOnMessageReceived;
    _pipe.Close();
    _pipe = null;
    IsOpen = false;
  }


  public override string ToString()
  {
    return _pipe?.ToString() ?? "Closed";
  }
}