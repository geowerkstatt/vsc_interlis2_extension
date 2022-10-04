import * as vscode from 'vscode';

export function activate(context: vscode.ExtensionContext) {
  const disposable = vscode.languages.registerImplementationProvider('INTERLIS2', {
      provideImplementation(document, position, token) {
        return [
          {
            uri: vscode.Uri.parse("https://models.interlis.ch/refhb24/Units.ili"),
            range: new vscode.Range(4, 11, 4, 15)
          },
          {
            uri: vscode.Uri.parse("https://models.interlis.ch/refhb24/Time.ili"),
            range: new vscode.Range(7, 16, 7, 20)
          }
        ]}
      });

      context.subscriptions.push(disposable);
}

export function deactivate() {
  console.log("INTERLIS Plugin deactivated.");
}
