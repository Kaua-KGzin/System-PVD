## ARCHNEXUS {VERSION}

Executavel unico para Windows x64. Nao precisa instalar .NET nem o backend: o Kestrel
sobe em loopback dentro do proprio processo, com banco SQLite local.

**Instalacao:** baixe o `ArchNexus.exe` e execute. Os dados ficam em `%LOCALAPPDATA%\ARCHNEXUS`.

**Requisitos:** Windows 10/11 x64 e o WebView2 Runtime (ja vem no Windows 11).

## O que mudou

{CHANGES}

## Antes de baixar: o Windows 11 pode bloquear este arquivo

O `ArchNexus.exe` **nao e assinado digitalmente**. Isso produz duas reacoes diferentes do
Windows, e so uma delas tem como contornar:

- **SmartScreen**: em qualquer Windows, aparece "O Windows protegeu o seu PC" na primeira
  execucao. Clique em **Mais informacoes** e depois em **Executar assim mesmo**. Acontece
  uma vez so.
- **Smart App Control**: exclusivo do Windows 11, **bloqueia a execucao sem oferecer saida**.
  Nao existe "executar assim mesmo" aqui: o aplicativo simplesmente nao abre.

O Smart App Control vem ligado em instalacoes limpas do Windows 11. Para rodar o ARCHNEXUS
numa maquina assim e preciso desliga-lo em **Seguranca do Windows -> Controle de aplicativos
e navegador -> Configuracoes do Smart App Control**.

> **Desligar o Smart App Control e irreversivel.** Uma vez desativado, ele so volta a ligar
> reinstalando o Windows. Pense duas vezes antes de fazer isso numa maquina de uso pessoal;
> prefira testar o ARCHNEXUS num terminal dedicado ao caixa.

Isso nao se resolve no codigo. A correcao real e assinar o executavel, o que exige um
certificado de code signing (OV ou EV) emitido por uma autoridade certificadora.

## Verificacao

Confira o arquivo baixado antes de executar:

```powershell
(Get-FileHash ArchNexus.exe -Algorithm SHA256).Hash
```

```
SHA256  {HASH}
```
