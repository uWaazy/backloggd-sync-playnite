# Backloggd Sync (Beta)


Plugin do Playnite que envia suas sessoes de jogo, status, nota e plataforma para o seu diario do [Backloggd](https://backloggd.com).

> **Beta.** Esta é a primeira versão pública. Ela funciona no dia a dia, mas ainda pode ter arestas. Por favor, reporte os problemas (veja [Como reportar problemas](#como-reportar-problemas)).
>
> **Nao oficial.** Sem relacao com o Backloggd nem aprovado por ele.

[English](README.md) | [Espanol](README.es.md)

## O que faz

- **Tempo por dia:** ao fechar um jogo, a sessao e somada ao dia dentro de um Log chamado **Playnite** no diario do jogo. Os seus proprios Logs nunca sao alterados.
- **Status:** o status de conclusao do Playnite vira Played, Playing ou Backlog (reconhecido pelo nome em portugues, ingles e espanhol). So e enviado quando muda no Playnite.
- **Nota:** a avaliacao do usuario do Playnite (0-100) vira nota de 1 a 10 (meias estrelas). So e enviada quando muda.
- **Plataforma:** jogos de PC (Steam, Epic, Game Pass...) viram *Windows PC*; consoles usam a propria plataforma. So e preenchida quando o Log ainda nao tem uma, entao a sua escolha e mantida.
- **Seguro por desenho:** cada envio grava um valor exato para o dia e depois le o site para confirmar. Falhas ficam na fila e sao repetidas; enviar a mesma coisa duas vezes nao soma o tempo duas vezes.
- **Vinculo:** os jogos sao reconhecidos por titulo exato e ano de lancamento. Se nao houver certeza, clique com o botao direito no jogo > **Backloggd Sync > Vincular jogo do Backloggd...** e escolha o correto; tudo o que estava pendente desse jogo e enviado em seguida.
- Menus e mensagens em portugues, ingles e espanhol.

## Instalar

1. Baixe o `.pext` em [Releases](../../releases) e abra-o (ou arraste para o Playnite).
2. Playnite > **Configuracoes > Extensoes > Generic > Backloggd Sync (Beta)**.
3. Clique em **Entrar no Backloggd**, entre na janela e feche. Deve aparecer *Conectado como <seu nome>*.

Cada release informa o SHA-256 do `.pext`, para voce comparar com o arquivo baixado.

## Configuracoes

- Enviar automaticamente ao fechar um jogo
- Enviar tempo / status / nota (cada um pode ser desligado)
- **Enviar pendencias agora** e um contador de pendencias ao vivo

Por jogo (botao direito > Backloggd Sync): vincular o jogo, enviar status e nota agora, ou nunca enviar o jogo.

## Como reportar problemas

Abra uma [issue](../../issues/new/choose) com as versoes do plugin e do Playnite e o que aconteceu. Anexe as linhas com `[BLS]` do arquivo `%AppData%\Playnite\extensions.log`. Elas trazem nomes de jogos e datas, mas nao senhas nem cookies; revise antes de compartilhar.

## Apoio

Se este plugin é útil para você e quiser apoiar o desenvolvimento contínuo:

[![Apoiar no Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/uWaazy)

## Licenca

MIT. Veja [LICENSE](LICENSE).
