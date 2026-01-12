// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.


// Array.from => on transforme "document.getElementsByClassName("btnSupp")" en tableau car pour faire un foreach on a besoin que ce soit un tableau
// (document.getElementsByClassName => renvoie une collection qui contien tous les boutons / liens qui sont sur la page 
Array.from(document.getElementsByClassName("btnSuppr")).forEach(lien => {

    // quand on fait un click sur le bouton ça lance une fonction JavaScript, qui affiche une petite popup "confirme"
    // addEventListener => ecouteur d'evenement
    lien.addEventListener("click", function (e) {
        // retourne un boolen et window.Confirm demande au navigateur d'afficher une popup avec un message, et ça attend que l'utilisateur confirme ou annule       
        let confirmeDelete = confirm("Êtes-vous sûr de vouloir supprimer cette catégorie ?");
        // si il confirme => true(par defaut) (on va vouloir supprimer) , si il annule => false (on ne supprime pas)
        if (!confirmeDelete) {
            e.preventDefault();// le comportement par defaut de l'evenement je ne le fait pas , fais donc false 
        }
    })
});