import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:vice/blocks/page.dart';
import 'package:vice/main.dart';
import 'package:vice/randoms.dart';
import 'performance/page.dart';
import 'settings/page.dart';
import 'soundboard/page.dart';
import 'channels/page.dart';

enum WindowPage { Channels, Soundboard, Settings, Performance, Blocks }

class Window extends StatefulWidget {
  const Window({super.key});

  @override
  State<Window> createState() => _WindowState();
}

class _WindowState extends State<Window> {
  WindowPage _currentPage = WindowPage.Channels;

  @override
  Widget build(BuildContext context) {
    context.watch<AppStateNotifier>();
    
    List<Map<String, dynamic>> buttons = [
      {"title": "Channels", "page": WindowPage.Channels},
      {"title": "Soundboard", "page": WindowPage.Soundboard},
      {"title": "Settings", "page": WindowPage.Settings},
      if (settings.monitor) {"title": "Performance", "page": WindowPage.Performance},
      {"title": "Blocks", "page": WindowPage.Blocks},
    ];
    
    return Scaffold(
      body: Column(
        children: [
          Align(
            alignment: Alignment.topCenter,
            child: Container(
              height: 70,
              decoration: BoxDecoration(
                color: bg_mid,
                borderRadius: const BorderRadius.only(
                  bottomLeft: Radius.circular(36),
                  bottomRight: Radius.circular(36),
                ),
              ),
              child: LayoutBuilder(
                builder: (context, constraints) {
                  List<double> widths = [];
                  for (var button in buttons) {
                    final TextPainter painter = TextPainter(
                      text: TextSpan(text: button["title"] as String, style: TextStyle(fontSize: 32)),
                      textDirection: TextDirection.ltr,
                    );
                    painter.layout();
                    widths.add(painter.width + 40.0);
                  }

                  const double overflowWidth = 48.0;
                  double totalWidth = 0.0;

                  List<int> visibleIndices = [];
                  List<int> overflowIndices = [];
                  for (int i = 0; i < buttons.length; i++) {
                    double buttonWidth = widths[i];
                    double potentialTotal = totalWidth + buttonWidth;
                    if (overflowIndices.isNotEmpty || i < buttons.length - 1) {
                      potentialTotal += overflowWidth;
                    }
                    if (potentialTotal <= constraints.maxWidth) {
                      visibleIndices.add(i);
                      totalWidth += buttonWidth;
                    } else {
                      overflowIndices.add(i);
                    }
                  }

                  List<Widget> children = [];
                  for (int i = 0; i < visibleIndices.length; i++) {
                    int idx = visibleIndices[i];
                    var button = buttons[idx];
                    children.add(
                      TextButton(
                        style: TextButton.styleFrom(
                          foregroundColor: _currentPage == button["page"] ? text : text_muted,
                          padding: const EdgeInsets.symmetric(vertical: 20, horizontal: 20),
                          alignment: Alignment.centerLeft,
                        ),
                        onPressed: () => setState(() => _currentPage = button["page"]),
                        child: Text(button["title"] as String, style: const TextStyle(fontSize: 32)),
                      ),
                    );
                    if (i < visibleIndices.length - 1) {
                      children.add(const SizedBox(width: 10));
                    }
                  }

                  if (overflowIndices.isNotEmpty) {
                    children.add(
                      PopupMenuButton<WindowPage>(
                        onSelected: (page) => setState(() => _currentPage = page),
                        itemBuilder: (context) => overflowIndices.map((i) {
                          var button = buttons[i];
                          return PopupMenuItem<WindowPage>(
                            value: button["page"] as WindowPage,
                            child: Text(button["title"] as String),
                          );
                        }).toList(),
                        icon: Icon(Icons.more_horiz, color: text),
                      ),
                    );
                  }
                  
                  return Row(
                    mainAxisAlignment: MainAxisAlignment.start,
                    children: children,
                  );
                },
              ),
            )
          ),

          Expanded(
            child: Container(
              color: bg_dark,
              child: switch (_currentPage) {
                WindowPage.Channels => ChannelsManagerDisplay(),
                WindowPage.Soundboard => SoundboardManagerDisplay(),
                WindowPage.Settings => SettingsPage(),
                WindowPage.Performance => PerformancePage(),
                WindowPage.Blocks => BlocksManagerDisplay()
              }
            )
          )
        ]
      )
    );
  }
}